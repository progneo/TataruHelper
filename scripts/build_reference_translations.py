"""Builds the reference translation index the app consults before translating.

The xivrus project (https://github.com/xivrus/xiv_ru_weblate) carries a
hand-made Russian translation of the game's own text, exported as XLIFF. A line
we find there is better than anything a machine will produce for it, and free
and instant besides - so it is worth carrying the answers around.

    python scripts/build_reference_translations.py
    python scripts/build_reference_translations.py --source <folder> --language ru

Streams the archive rather than unpacking it: the export is around a gigabyte
on disk, and the two files that have to be read together sit next to each other
in it, so nothing needs to be kept.
"""
import argparse
import io
import re
import sqlite3
import sys
import tarfile
import urllib.request
from pathlib import Path

ARCHIVE_URL = "https://codeload.github.com/xivrus/xiv_ru_weblate/tar.gz/refs/heads/main"

DEFAULT_OUTPUT = Path(__file__).resolve().parent.parent / "TataruHelper" / "Resources" / "ReferenceTranslations.db"

UNIT = re.compile(
    r'<trans-unit id="([^"]+)"[^>]*>\s*<source>.*?</source>\s*'
    r'<target(?: state="([^"]*)")?>(.*?)</target>',
    re.DOTALL)

ENTITIES = [('&lt;', '<'), ('&gt;', '>'), ('&quot;', '"'), ('&apos;', "'"), ('&amp;', '&')]

# Markup that only styles what is already there. The words around it are drawn
# exactly as written, so taking the tags out leaves a line that matches what we
# read; throwing the line away over them costs 140,000 lines for nothing.
#
#   1A          emphasis on and off
#   48, 49      the pair wrapping a highlighted term
#   17, 1F      page break, word break
#   1B, 1C, 60  colour, and a cue that draws nothing
FORMATTING = re.compile(
    r'<var (?:1A|48|49|17|1F|1B|1C|60)[^>]*>'
    r'|</?(?:color2|glow2|color|glow)[^>]*>')

# Renders as a line break, and we read wrapped dialogue joined.
LINE_BREAK = re.compile(r'<nl>')

# Anything the game substitutes as it draws: gender agreement, the player's
# name, a number it works out, the name of a duty. What reaches the screen has
# those filled in already, so the line cannot be matched and has to go to a
# translator instead.
#
# Recognised by being a <var>: everything the game resolves is one. Sound cues
# - <sigh>, <click>, <gasp> and some thirty others - are drawn as the literal
# text they look like, so they stay, and a line is not thrown away over them.
DYNAMIC = re.compile(r'<var [^>]*>')

# The player's own name, which the game writes in as it draws. A line whose
# only substitution is this can still be used: the fixed text pins it down and
# the name is known once the character is loaded, so it is stored as a pattern
# with the name punched out and filled in at runtime.
PLAYER = re.compile(r'<var 2C .*?\)\) [0-9A-F]{2} /var>')

# Stands in for the name inside a stored pattern. A control character, so it
# cannot collide with anything the game would write.
PLAYER_PLACEHOLDER = '\x01'

# Battle and cutscene lines carry their speaker inside the text, as
# "(-Ixali Occultists-)O mournful voice of creation!". We read the speaker from
# its own node and the line from another, so the line alone is what has to
# match - with the wrapper left on, none of these were ever found.
SPEAKER_PREFIX = re.compile(r'^\(-([^)]{0,60})-\)')

# The game's own list of who everyone is, stored as "Name<tab>Title".
NPC_SHEET = 'ENpcResident'

# The game writes a typographic apostrophe in names - Y’shtola - and a plain
# one elsewhere for the same character. Matching has to see them as one.
APOSTROPHES = str.maketrans({'’': "'", 'ʼ': "'", '‘': "'"})


def fold_apostrophes(text):
    return text.translate(APOSTROPHES)


# Agreement with the player's gender: <var 08 E905 ((feminine)) ((masculine))>,
# feminine first. English rarely needs it - "adventurer" has no gender - so
# these are lines where only the Russian is conditional, and the character's
# gender is readable from the game, so both renderings are kept.
#
# E905 alone. The other condition codes ask about things that cannot be
# answered here - E4EB02EB03 distinguishes a joystick from a mini-joystick -
# and guessing at them would put the wrong words on screen.
GENDERED = re.compile(r'<var 08 E905 \(\((.*?)\)\) \(\((.*?)\)\) /var>')

FEMININE, MASCULINE = 'f', 'm'

# Quest text is stored with its internal key ahead of a <tab>. The key also
# names the speaker - TEXT_MANFST005_00445_200250_HYDAELYN - which is worth
# remembering if speaker names are ever wanted.
KEY_SEPARATOR = '<tab>'


def unescape(text):
    for entity, char in ENTITIES:
        text = text.replace(entity, char)
    return text


def normalize(text):
    """Reduces a stored line to the form the reader hands us.

    The game wraps dialogue across lines and we read it joined, so whitespace
    cannot be trusted to match.

    Only the first <tab> separates the key from the text; any after it are
    tabs within the line itself. They are whitespace once the game has drawn
    them, so they become whitespace here - left alone they would neither match
    anything nor be fit to show, and a line reading "<tab>" in the middle is
    exactly what a reader would report as a bug.
    """
    separator = text.find(KEY_SEPARATOR)
    if separator >= 0:
        text = text[separator + len(KEY_SEPARATOR):]

    text = FORMATTING.sub('', text)
    text = LINE_BREAK.sub(' ', text)
    text = SPEAKER_PREFIX.sub('', text.lstrip())
    return ' '.join(text.replace(KEY_SEPARATOR, ' ').split())


def strip_key(text):
    """Drops the internal key quest text carries ahead of a <tab>."""
    separator = text.find(KEY_SEPARATOR)
    return text[separator + len(KEY_SEPARATOR):] if separator >= 0 else text


def parse_units(raw):
    return {m.group(1): unescape(m.group(3)) for m in UNIT.finditer(raw)}


class Index:
    def __init__(self):
        self.pairs = {}
        self.patterns = {}
        self.speakers = {}
        self.gendered = {}
        self.conflicts = 0
        self.skipped_dynamic = 0

    def add_speaker(self, english, translated):
        """A character's name as the translators render it.

        Two sources agree on the shape and not always on the wording: the
        game's own roster, and the wrapper that battle and cutscene lines put
        around the speaker. The roster is read first and the wrapper second, so
        a name that appears in dialogue wins - that is the one being read out
        loud beside the line.
        """
        english = fold_apostrophes(' '.join(english.split()))
        translated = ' '.join(translated.split())

        if not english or not translated or english == translated:
            return
        if DYNAMIC.search(english) or DYNAMIC.search(translated):
            return

        # "???" is the game keeping someone's identity back, and the Russian
        # wrapper gives it away - it named a stranger on a boat "Дружелюбный
        # пассажир" while the English still read "???". A label with no letters
        # in it is a placeholder, not a name, and translating it spoils the
        # scene it was hiding.
        if not any(character.isalpha() for character in english):
            return

        self.speakers[english] = translated

    def add(self, english, translated):
        english, translated = normalize(english), normalize(translated)

        if not english or not translated or english == translated:
            return

        # One placeholder each side and nothing else left to substitute: the
        # line is usable as a pattern. More than one and the pieces between
        # them stop pinning it down reliably, so it is not worth the risk.
        english_player = PLAYER.sub(PLAYER_PLACEHOLDER, english)
        translated_player = PLAYER.sub(PLAYER_PLACEHOLDER, translated)
        is_pattern = (english_player.count(PLAYER_PLACEHOLDER) == 1
                      and translated_player.count(PLAYER_PLACEHOLDER) == 1)

        if is_pattern:
            english, translated = english_player, translated_player
            target = self.patterns
        else:
            target = self.pairs

        # Gender agreement, and nothing else left to substitute once it is
        # resolved: keep the line both ways and choose when the character is
        # known. Both sides can carry it - English says "this woman" against
        # "this man" as readily as Russian declines around it - so the line is
        # stored under the English each character would actually hear.
        if GENDERED.search(english) or GENDERED.search(translated):
            feminine_english = GENDERED.sub(lambda m: m.group(1), english)
            masculine_english = GENDERED.sub(lambda m: m.group(2), english)
            feminine = GENDERED.sub(lambda m: m.group(1), translated)
            masculine = GENDERED.sub(lambda m: m.group(2), translated)

            if not (DYNAMIC.search(feminine_english) or DYNAMIC.search(masculine_english)
                    or DYNAMIC.search(feminine) or DYNAMIC.search(masculine)):
                self.gendered[(feminine_english, FEMININE)] = feminine
                self.gendered[(masculine_english, MASCULINE)] = masculine
                return

        if DYNAMIC.search(english) or DYNAMIC.search(translated):
            self.skipped_dynamic += 1
            return

        existing = target.get(english)
        if existing is None:
            target[english] = translated
        elif existing != translated:
            # The same English line said in two places, rendered differently.
            # Keeping the first is arbitrary but stable, and the alternative -
            # dropping both - loses more than it protects.
            self.conflicts += 1


def iter_sheets(members, language):
    """Yields (english, translated) raw file contents for each sheet."""
    pending_english = {}
    pending_translated = {}

    for name, read in members:
        parts = name.split('/')
        if len(parts) < 2:
            continue

        folder, filename = '/'.join(parts[:-1]), parts[-1]

        if filename == 'en.xlf':
            counterpart = pending_translated.pop(folder, None)
            if counterpart is None:
                pending_english[folder] = parse_units(read())
            else:
                yield folder, parse_units(read()), counterpart
        elif filename == f'{language}.xlf':
            counterpart = pending_english.pop(folder, None)
            if counterpart is None:
                pending_translated[folder] = read()
            else:
                yield folder, counterpart, read()


def stream_archive(source):
    if source == 'github':
        print(f'downloading {ARCHIVE_URL}', flush=True)
        request = urllib.request.Request(ARCHIVE_URL, headers={'User-Agent': 'TataruHelper'})
        stream = urllib.request.urlopen(request, timeout=300)
        archive = tarfile.open(fileobj=stream, mode='r|gz')
        for member in archive:
            if member.isfile() and member.name.endswith('.xlf'):
                extracted = archive.extractfile(member)
                if extracted is not None:
                    payload = extracted.read()
                    yield member.name, lambda payload=payload: payload.decode('utf-8-sig', 'replace')
        archive.close()
        return

    root = Path(source)
    if not root.exists():
        raise SystemExit(f'no such folder: {root}')

    for path in sorted(root.rglob('*.xlf')):
        yield (path.as_posix(),
               lambda path=path: path.read_text(encoding='utf-8-sig', errors='replace'))


def build(source, language, output):
    index = Index()
    sheets = 0

    for folder, english, translated in iter_sheets(stream_archive(source), language):
        sheets += 1
        if isinstance(translated, str):
            translated = parse_units(translated)
        if isinstance(english, str):
            english = parse_units(english)

        is_roster = folder.rsplit('/', 1)[-1] == NPC_SHEET

        for unit_id, translated_text in translated.items():
            english_text = english.get(unit_id)
            if english_text is None:
                continue

            if is_roster:
                # "Name<tab>Title" - only the name is ever spoken.
                index.add_speaker(english_text.split(KEY_SEPARATOR)[0],
                                  translated_text.split(KEY_SEPARATOR)[0])
                continue

            english_speaker = SPEAKER_PREFIX.match(strip_key(english_text))
            translated_speaker = SPEAKER_PREFIX.match(strip_key(translated_text))
            if english_speaker and translated_speaker:
                index.add_speaker(english_speaker.group(1), translated_speaker.group(1))

            index.add(english_text, translated_text)

        if sheets % 500 == 0:
            print(f'  {sheets} sheets, {len(index.pairs)} lines', flush=True)

    print(f'sheets read      : {sheets}')
    print(f'lines indexed    : {len(index.pairs)}')
    print(f'name patterns    : {len(index.patterns)}')
    print(f'speaker names    : {len(index.speakers)}')
    print(f'gendered lines   : {len(index.gendered) // 2}')
    print(f'skipped (markup) : {index.skipped_dynamic}')
    print(f'conflicting      : {index.conflicts}')

    if not index.pairs:
        raise SystemExit('nothing indexed - the export layout has probably changed')

    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists():
        output.unlink()

    db = sqlite3.connect(output)
    db.execute('PRAGMA journal_mode = OFF')
    db.execute('CREATE TABLE line (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID')
    # Lines addressed to the player, with the name punched out. Read whole and
    # filled in once the character is known, so no index is wanted here.
    db.execute('CREATE TABLE pattern (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID')
    # Who is speaking, as the translators render them.
    db.execute('CREATE TABLE speaker (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID')
    # Lines whose Russian agrees with the player's gender, kept both ways.
    db.execute('CREATE TABLE gendered (source TEXT NOT NULL, feminine INTEGER NOT NULL, translated TEXT NOT NULL, PRIMARY KEY (feminine, source)) WITHOUT ROWID')
    db.execute('CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL)')
    db.executemany('INSERT INTO line VALUES (?, ?)', index.pairs.items())
    db.executemany('INSERT INTO pattern VALUES (?, ?)', index.patterns.items())
    db.executemany('INSERT INTO speaker VALUES (?, ?)', index.speakers.items())
    db.executemany('INSERT OR IGNORE INTO gendered VALUES (?, ?, ?)',
                   [(source, 1 if sex == FEMININE else 0, translated)
                    for (source, sex), translated in index.gendered.items()])
    db.executemany('INSERT INTO meta VALUES (?, ?)', [
        ('language', language),
        ('source', ARCHIVE_URL if source == 'github' else str(source)),
        ('lines', str(len(index.pairs))),
        ('patterns', str(len(index.patterns))),
        ('speakers', str(len(index.speakers))),
        ('gendered', str(len(index.gendered))),
        ('playerPlaceholder', PLAYER_PLACEHOLDER),
    ])
    db.commit()
    db.execute('VACUUM')
    db.close()

    print(f'\nwrote {output} ({output.stat().st_size / 1024 / 1024:.1f} MB)')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', default='github',
                        help="'github' (default) or a folder holding the exd export")
    parser.add_argument('--language', default='ru', help='target language code, as named in the export')
    parser.add_argument('--output', type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()

    build(args.source, args.language, args.output)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
