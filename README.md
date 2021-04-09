## About
This tool copies as much metadata as possible from a module (.mod,.xm,.s3m,.it) music file to the ID3 tags of an MP3 file. It doesn't actually convert the mod to MP3 (see below for info on that), but can be used independently of whatever your conversion process is. And it is a command line program, so can be automated alongside conversion.

The purpose of this tool was for me to try to get a handle on my tag-less mod conversions by including all the metadata from the original module as was possible for both archival purpose and better library management. Then as I was working on this project, I came up with an acceptable way to convert all my mods to MP3 at once, although some of them will still require a more manual conversion ultimately if they're to sound as they should.

This was the first time I used C# and the first time in a while that I used .Net, so my apologies if it's not pretty code or has a stupid bug. Uses .Net 5, libopenmpt 0.5.6, tagligsharp 2.2.0, and Command Line Parser 2.8.0.

### The metadata
Here's what this program copies over or adds as ID3 metadata.
- **Title**: As long as the original mod file has this.
- **Tracker**: The ID3 tag for composer is filled with the name of the tracker software used.
- **Artist**: Was unfortunately not standard metadata in the most common mod formats and as such can only be guessed at by regex matching on the message fields, which is about 33% accurate.
- **Date**: Only became standard metadata with Impulse Tracker formats, so those are the only files that copy date precisely. The program will also try to find the date using regexes on the message fields, which is about 60% accurate. And it can also (optionally) copy the date from the modification date of the mod file if you happen to have original copies.
- **Messages**: The number of channels and patterns, the original song duration, comments (if any), list of instruments (if any), and list of samples are all concatenated and added as ID3 comments. As was frequently the case and depending on the mod format, mod authors would often put their song notes in the instrument or sample list. So, I felt the inclusion of all of these was imperative for archival purposes.
- **Album Art**: An optional feature to add embedded album art into the MP3 file with the chosen image based on the tracker used. Some default images are included for the four most common mod formats, but they are external in case you wish to overwrite them with your own.
- **Original Filename**: An optional feature to add a TXXX ID3 tag with the mod's original filename.

## Usage
Can be passed a mod file path as a single argument if you only want to output the metadata to console or a file. Otherwise, additionally pass an MP3 file path using the argument format below to copy the metadata directly from MOD to MP3.

### Arguments
- `-i, --mod` Required. Input module file path.
- `-o, --mp3` Output MP3 file path. Writes metadata to stdout if omitted.
- `--id3v2` Version of ID3v2 to use, a value between 2 and 4. Effects compatibility with other apps. Defaults to v2.3 or the file's existing ID3 version if omitted.
- `-a, --album-art` Turns on adding album art image to the MP3 file based on the tracker used.
- `-r, --rename` Turns on renaming MP3 file based on Artist and Title.
- `-f, --save-original-filename` Copies the module's filename to a TXXX ID3 field. Useful in combination with the rename option.
- `-m, --use-mod-date` Falls back to using the input mod's file modified date for the ID3 date.

### Examples

```dos
mod_metadata_extractor mod_file.mod
mod_metadata_extractor mod_file.mod > mod_file.txt
mod_metadata_extractor -i mod_file.mod -o mp3_file.mp3 -arfm
mod_metadata_extractor --mod=mod_file.mod --mp3=mp3_file.mp3 --id3v2=3
```

## Converting Mods to MP3
Module music can be particularly difficult to convert because (like MIDIs) much of the playback is left to the player software, the various file formats have different features and are non-standardized, and thus all the commands, effects, and resampling are open to interpretation. I started listening to mods in the days of Windows 98, so I grew to be partial to the sound of ModPlug Player, the most popular module player of the time. But some mods will even explicitly state in their comments what player is intended to be used for the best playback.

The best method I've found so far for properly converting mods to a suitable compressed sound format is to open them up them up in OpenMPT, resample any poor-quality (8-bit hissing, bad loops, clicks, etc.) samples, adjust the mixing and volume settings to your liking, and export to whatever format you prefer. Or you can loopback record to an audio editor like Audacity or Audition if you wish to do some further quality control like monitoring clipping. However, all this takes considerable time.

### Automating conversion with ffmpeg
You can also use ffmpeg to run a batch conversion of your mod library to MP3 (or the format of your choice). The current version of ffmpeg (4.3.2) uses the libopenmpt library to playback mods, which is the same library this project uses to extract the metadata. It produces playback very similar to that of ModPlug Player and will convert at least 95% of mods acceptably. However, the sound it produces is very muddy and bland by default, so I've come up with an audio filter chain for ffmpeg that makes the output a lot more aurally pleasing (at least imho).

```dos
ffmpeg -i "https://api.modarchive.org/downloads.php?moduleid=69085#hypercontrol_v3_0.s3m" -af superequalizer=1b=0.53:2b=0.62:3b=0.66:4b=0.55:5b=0.45:6b=0.38:7b=0.41:8b=0.51:9b=0.63:10b=0.75:11b=0.85:12b=0.92:13b=0.96:14b=0.92:15b=0.83:16b=0.66:17b=0.49:18b=0.45,bass=gain=3.5:frequency=80:width_type=h:width=80,extrastereo=m=2.0,alimiter=level_in=1.6:limit=0.9:level=disabled,adeclick=t=10 -q:a 3 -id3v2_version 3 "hypercontrol_v3_0.s3m.mp3"
```

#### Explanation of the command
- `-i "input mod file path"` This is where you put your input file. ffmpeg supports urls as well, so I used that above just for demo purposes. Good practice to wrap your file paths in quotes.
- `-af` Tells ffmpeg to use the following audio filters
- `superequalizer=1b=0.53: ... 18b=0.45` An 18-band equalizer that produces a sound similar to that of the "Clear" preset in ModPlug Player. Basically increases the treble a lot, the bass a little, and lowers the mids. It's all normalized to 100% of the previous amplitude to prevent clipping, but we'll recover that volume later.
- `bass=gain=3.5:frequency=80:width_type=h:width=80` This is a bass boost filter. The center frequency is a little bit lower than the default as I like my bass more rumbly than punchy. 350% amplitude was about all I could do without risking possible clipping distortion.
- `extrastereo=m=2.0` Widens the stereo separation similar to how 200% stereo would sound in MPP. I think it makes most mods a lot more enveloping and helps any upmixing for surround speakers (e.g. using "Speaker Fill" filters). May be a bit overwhelming if you're only using headphones, though. You could also try the `stereowiden` ffmpeg filter to achieve a similar surround effect, although I've not tried so yet.
- `alimiter=level_in=1.6:limit=0.9:level=disabled` The limiter is where we recover our volume lost during the EQ without causing any clipping. It should produce an ITU loudness somewhere around -10dB for most mods, which is not quite as blown out as most popular music but is still a little louder than the input mod. I chose an amplified limiter instead of volume normalization (i.e. using `loudnorm` filter) so any quiet songs aren't totally destroyed by loudness, and also because true normalization is a two-pass process that will complicate the conversion command. However, you might find some mods with stupidly-set volume that might do better with a higher or lower `level_in` value.
- `adeclick=t=10` This is a declick filter but it's kinda garbage compared to the one in Audition, so I set its threshold really high to only remove the most egregious clicks. Otherwise, it ends up finding 500,000 clicks per song and turns the output into an overly smoothed-out mess. It's better to fix any clicks in source module samples. Also, this filter significantly slows down the conversion (like 2x), so you might want to skip it altogether if you have a lot of files.
- `apad=pad_dur=1.5,silenceremove=stop_periods=1:stop_duration=3:stop_threshold=-60dB:stop_silence=3` I didn't include these two filters in the above command but will here for your consideration. They are supposed to "normalize" the silence at the end of a song by adding 1.5 seconds of silence and then removing 3 seconds of silence, such that songs end with between 1.5 and 3 seconds of silence, AKA an acceptable song gap. However, the silenceremove filter is really poorly made and/or documented (as you can tell from the two parameters that seem to have the same function), and it ended up really screwing up several songs with sudden small cuts or even just ending the song early. So, use with caution unless this filter improves.
- `-q:a 3` This is the quality setting for the MP3 encoder. It's is analogous to calling Lame with `-V 3` for variable bit-rate around 170kbps. You can look up how to set CBR, ABR, and recommended bitrates in the ffmpeg manual and elsewhere.
- `-id3v2_version 3` Sets the ID3 tag version to 2.3. The default is 2.4 right now, but I've found it to be unsupported by some of my software. Change as is necessary for you.
- `"output mp3 file path"` The output file requires no switch name and generally comes at the end of the command arguments. Give it a .mp3 extension to tell ffmpeg to output in MP3 format. You'll probably want the output filename to be mostly the same as the input filename so that this metadata program can be put directly after the mod-to-mp3 conversion command in a batch file, so probably just add a .mp3 extension alone.

![18-Band Module Conversion EQ Settings](https://raw.githubusercontent.com/slserpent/mod-metadata-extractor/main/images/eq.png)

#### Batch conversion creation
Of course, you can use batch loops and variables to iterate through all the modules you wish to convert if you can remember the syntax for that (I never can). Or (shameless plug), I have a little tool that can make batch files for you: https://www.snakebytestudios.com/projects/apps/file-lister/

### Example Results
Directory with album art thumbnails and MediaInfo output
![Directory with album art thumbnails and MediaInfo output](https://raw.githubusercontent.com/slserpent/mod-metadata-extractor/main/images/example.png)
Windows directory with MP3s in detail view after metadata copied
![Windows directory with MP3s in detail view after metadata copied](https://raw.githubusercontent.com/slserpent/mod-metadata-extractor/main/images/example2.png)

## TODO
- Use modarchive API to search by filename or MD5 hash and obtain artist name, genre, and modarchive id?
	- this data isn't available for every mod, though
	- requires api key signup (so maybe not suitable for end-user)
	- https://modarchive.org/index.php?xml-api