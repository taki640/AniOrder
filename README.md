# AniOrder
A CLI application to list anime, manga or light novels from Anilist in release order.  
*I made this because I wanted to know in which order to watch Fate.*

## Usage
```
AniOrder <Anime/Manga> [options]
```

### Arguments:
  - `<Anime/Manga>`  
  Anilist URL or ID

### Options:
  - `-f, --file <file>`  
  The path to write the results of the search. If empty no file is created.
  - `-r, --relations <relations>`  
  Types of relations allowed for searching, separated by commas. Allowed values: ADAPTATION, PREQUEL, SEQUEL, PARENT, SIDE_STORY, CHARACTER, SUMMARY, ALTERNATIVE, SPIN_OFF and OTHER [default: PREQUEL, SEQUEL, PARENT, SIDE_STORY, ALTERNATIVE, SPIN_OFF].
  - `-mt, --media-type <media-type>`  
  The type of media allowed for searching. One of: ANY, MANGA, ANIME [default: ANIME].
  - `--version`  
  Show version information.
  - `-?, -h, --help`  
  Show help and usage information.
