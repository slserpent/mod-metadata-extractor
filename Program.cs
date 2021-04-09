using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CommandLine;

namespace Mod_Metadata_Extractor {
    
    class Program {
        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint openmpt_get_library_version();

        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern uint* openmpt_module_ext_create_from_memory(IntPtr filedata, uint filesize, openmpt_log_func logfunc, void* loguser, openmpt_error_func errfunc, void* erruser, int* error, IntPtr error_message, void* ctls);
        unsafe delegate void openmpt_log_func(String message, void* user);
        unsafe delegate int openmpt_error_func(int error, void* user);

        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern double openmpt_module_get_duration_seconds(void* mod);

        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern IntPtr openmpt_module_get_metadata(void* mod, String key);

        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int openmpt_module_get_num_instruments(void* mod);

        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern IntPtr openmpt_module_get_instrument_name(void* mod, int index);

        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int openmpt_module_get_num_samples(void* mod);

        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern IntPtr openmpt_module_get_sample_name(void* mod, int index);

        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int openmpt_module_get_num_channels(void* mod);

        [DllImport("libopenmpt.dll", CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int openmpt_module_get_num_patterns(void* mod);

        static int Main(string[] args) {
            /*
            uint version = openmpt_get_library_version();
            Console.WriteLine((version >> 24) + "." + (version >> 16) + "." + (version >> 8));
            */

            /*
            DateTime parsed_date;
            foreach (var culture in System.Globalization.CultureInfo.GetCultures(CultureTypes.InstalledWin32Cultures)) {
                Console.Write(culture.Name + "\t");
            }
            Console.Write("\r\n");
            foreach (var line in File.ReadAllLines(@"D:\My Documents\Projects\mod_metadata\Mod Metadata Extractor\test_dates.txt")) {
                Console.Write(line);
                foreach (var culture in System.Globalization.CultureInfo.GetCultures(CultureTypes.InstalledWin32Cultures)) {
                    if (DateTime.TryParse(line, culture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out parsed_date)) {
                        Console.Write("\t" + parsed_date.ToShortDateString());
                    } else {
                        Console.Write("\tFail");
                    }
                }
                Console.Write("\r\n");
            }
            return 0;
            */

            //can be passed a single file path as an argument as well
            if (args.Length == 1 && args[0].StartsWith('-') == false) {
                args[0] = args[0].Insert(0, "--mod=");
            }

            var cmd_parser = new CommandLine.Parser(with => with.HelpWriter = null);
            var parse_result = cmd_parser.ParseArguments<Options>(args);
            if (parse_result.Tag == ParserResultType.Parsed) {
                return parse_result.MapResult(
                    options => run_program(options),
                    _ => 1);
            } else {
                parse_result.WithNotParsed(x => {
                    var helpText = CommandLine.Text.HelpText.AutoBuild(parse_result, h => {
                        var description = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>();
                        if (description != null) {
                            h.Copyright = description.Description;
                        } else {
                            h.Copyright = "";
                        }
                        var exe_name = Path.GetFileName(Assembly.GetExecutingAssembly().GetName().Name);
                        h.AddPostOptionsLine("Examples:");
                        h.AddPostOptionsLine(exe_name + " mod_file.mod");
                        h.AddPostOptionsLine(exe_name + " mod_file.mod > mod_file.txt");
                        h.AddPostOptionsLine(exe_name + " -i mod_file.mod -o mp3_file.mp3 -arfm");
                        h.AddPostOptionsLine(exe_name + " --mod=mod_file.mod --mp3=mp3_file.mp3 --id3v2=3");
                        h.AutoVersion = false;
                        return CommandLine.Text.HelpText.DefaultParsingErrorsHandler(parse_result, h);
                    }, e => e);
                    Console.WriteLine(helpText);
                });
                return 1;
            }
            
        }

        unsafe static int run_program(Options opts) {
            byte[] modfile;
            try {
                modfile = System.IO.File.ReadAllBytes(opts.ModFile);
            } catch (Exception err) {
                Console.WriteLine("Error loading mod file: " + err.Message);
                return 1;
            }
            int modfilelen = modfile.Length;
            IntPtr buf = Marshal.AllocHGlobal(modfilelen);
            Marshal.Copy(modfile, 0, buf, modfilelen);

            int error = 0;
            void* mod = openmpt_module_ext_create_from_memory(buf, Convert.ToUInt32(modfilelen), openmpt_log, null, null, null, &error, IntPtr.Zero, null);

            if (error > 0) { 
                //openmpt should give error message through delegated log function
                return 1;
            }

            String title = marshal_to_utf8(openmpt_module_get_metadata(mod, "title"));
            String artist = marshal_to_utf8(openmpt_module_get_metadata(mod, "artist")); //most module formats don't have this field
            String composer = marshal_to_utf8(openmpt_module_get_metadata(mod, "type_long"));
            String mod_type = marshal_to_utf8(openmpt_module_get_metadata(mod, "type"));
            String date = marshal_to_utf8(openmpt_module_get_metadata(mod, "date"));
            String message = marshal_to_utf8(openmpt_module_get_metadata(mod, "message_raw"));
            int channels = openmpt_module_get_num_channels(mod);
            int patterns = openmpt_module_get_num_patterns(mod);

            String instruments = "";
            int instrument_count = openmpt_module_get_num_instruments(mod);
            if (instrument_count > 0) {
                String[] instrument_array = new String[instrument_count];
                int instrument_chars = 0;
                for (int i = 0; i < instrument_count; i++) {
                    instrument_array[i] = marshal_to_utf8(openmpt_module_get_instrument_name(mod, i));
                    instrument_chars += instrument_array[i].Length;
                }
                if (instrument_chars > 0) instruments = String.Join("\n", instrument_array).Trim();
            }

            String samples = "";
            int sample_count = openmpt_module_get_num_samples(mod);
            if (sample_count > 0) {
                String[] sample_array = new String[sample_count];
                int sample_chars = 0;
                for (int i = 0; i < sample_count; i++) {
                    sample_array[i] = marshal_to_utf8(openmpt_module_get_sample_name(mod, i));
                    sample_chars += sample_array[i].Length;
                }
                if (sample_chars > 0) samples = String.Join("\n", sample_array).Trim();
            }

            //returns song duration as seconds. convert it to [hh:]mm:ss
            double song_length = openmpt_module_get_duration_seconds(mod);
            uint song_length_hr = Convert.ToUInt32(Math.Floor(song_length / 3600));
            uint song_length_min = Convert.ToUInt32(Math.Floor(song_length / 60));
            uint song_length_sec = Convert.ToUInt32(song_length % 60);
            String duration = "";
            if (song_length_hr > 0) {
                duration = song_length_hr + ":" + song_length_min.ToString().PadLeft(2, '0') + ":" + song_length_sec.ToString().PadLeft(2, '0');
            } else {
                duration = song_length_min + ":" + song_length_sec.ToString().PadLeft(2, '0');
            }

            if (artist.Length == 0) {
                //try to find the artist in the mod messages
                String[] message_fields = { message, instruments, samples, title };
                foreach (String field in message_fields) {
                    if (field.Length > 0) {
                        try {
                            Match artist_match = Regex.Match(field, @"^\s*(?:by|author|composed by|written by|artist|composer|\(c\) \d+)[:>\-\s]+(.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                            if (artist_match.Success) {
                                artist = artist_match.Groups[1].Value;
                                break;
                            }
                        } catch (ArgumentException err) {
                            Console.WriteLine("Error with artist regex: " + err.Message);
                        }
                    }
                }
            }

            //dates are returned as ISO-8601
            DateTime output_date = new DateTime();
            Boolean has_date = false;
            if (date.Length > 0 && DateTime.TryParseExact(date, "yyyy-MM-ddTHH:mm:ssK", new CultureInfo("en-US"), DateTimeStyles.AdjustToUniversal, out output_date)) {
                has_date = true;
            } else {
                //try to find the date in the mod messages

                //this is apparently some kind of anonymous function. using since break doesn't have depths in c#
                new Action(() => {
                    foreach (String field in new String[] { message, instruments, samples, title }) {
                        if (field.Length > 0) {
                            //matching various forms of numberic-only dates
                            Match date_match = Regex.Match(field, @"\b(?:\d{2}|\d{4})[\.\-/]\d{1,2}[\.\-/](?:\d{2}|\d{4})\b", RegexOptions.Multiline);
                            if (date_match.Success) {
                                //in my tests, these three cultures in this order parsed >95% of the dates passed in
                                //US english, finnish, swedish
                                foreach (String culture in new String[] { "en-US", "fi", "sv" }) {
                                    if (DateTime.TryParse(date_match.Groups[0].Value, new CultureInfo(culture), DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out output_date)) {
                                        has_date = true;
                                        return;
                                    }
                                }
                            } else {
                                //matching various forms of alphabetic month name dates
                                date_match = Regex.Match(field, @"\b(?:\d{1,2}[.\-/\s]+)?(jan(?:uary)|feb(?:ruary)|mar(?:ch)|apr(?:il)|may|june?|july?|aug(?:ust)|sept?(?:ember)|oct(?:ober)|nov(?:ember)|dec(?:ember))(?:[.\-/,\s]+\d{1,2})?[.\-/,\s]+(?:\d{2}|\d{4})\b", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                                if (date_match.Success) {
                                    foreach (String culture in new String[] { "en-US", "fi", "sv" }) {
                                        if (DateTime.TryParse(date_match.Groups[0].Value, new CultureInfo(culture), DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out output_date)) {
                                            has_date = true;
                                            return;
                                        }
                                    }
                                } else {
                                    //matching years only. for some reason the tryparse fails at these in every culture.
                                    date_match = Regex.Match(field, @"\b(?:19|20)\d\d\b", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                                    if (date_match.Success) {
                                        if (DateTime.TryParseExact(date_match.Groups[0].Value, "yyyy", new CultureInfo("en-US"), DateTimeStyles.AdjustToUniversal, out output_date)) {
                                            has_date = true;
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                })();
            }
            //if we still don't have a date, try to get it from the file's modified date
            //some downloads/copies include the modified date from the original files
            if (has_date == false && opts.use_mod_date) {
                output_date = File.GetLastWriteTimeUtc(opts.ModFile);
                has_date = true;
            }

            String output_comments = "";
            output_comments += "CHANNELS: " + channels + "\n";
            output_comments += "PATTERNS: " + patterns + "\n";
            output_comments += "DURATION: " + duration + "\n";
            if (message.Length > 0) output_comments += "MESSAGE:\n" + message.Trim() + "\n";
            if (instruments.Length > 0) output_comments += "INSTRUMENTS:\n" + instruments + "\n";
            if (samples.Length > 0) output_comments += "SAMPLES:\n" + samples + "\n";

            if (opts.MP3File != null) {
                //write metadata to mp3 file

                //load the mp3 file
                TagLib.File mp3file;
                try {
                    mp3file = TagLib.File.Create(opts.MP3File);
                } catch (Exception err) {
                    Console.WriteLine("Error loading mp3 file: " + err.Message);
                    return 1;
                }

                //get a tag object
                TagLib.Id3v2.Tag.DefaultVersion = 3;
                TagLib.Id3v2.Tag mp3_tags;
                var temp_tags = mp3file.GetTag(TagLib.TagTypes.Id3v2, true);
                if (temp_tags == null) {
                    Console.WriteLine("Could not create ID3v2 tags for this file type.");
                    return 1;
                } else {
                    mp3_tags = (TagLib.Id3v2.Tag)temp_tags;
                }

                //set id3 version
                if (opts.id3_version != null && opts.id3_version >= 2 && opts.id3_version <= 4) {
                    mp3_tags.Version = (byte)opts.id3_version;
                }

                //set the tags
                if (title.Length > 0) mp3_tags.Title = title;
                if (artist.Length > 0) mp3_tags.Performers = new string[] { artist };
                if (composer.Length > 0) mp3_tags.Composers = new string[] { composer };
                if (has_date) {
                    if (mp3_tags.Version == 3) {
                        mp3_tags.Year = Convert.ToUInt32(output_date.ToString("yyyy"));
                        mp3_tags.SetTextFrame("TDAT", output_date.ToString("ddMM"));
                    } else if (mp3_tags.Version == 4) {
                        //"o" format should be ISO-8601
                        mp3_tags.SetTextFrame("TDRC", output_date.ToString("o"));
                    }
                }

                //remove ffmpeg putting comments in TXXX instead of COMM. but don't disturb other TXXX frames. we're going to use our own COMM field anyways.
                //https://trac.ffmpeg.org/ticket/8996, also https://trac.ffmpeg.org/ticket/7967 lol
                remove_usertext_tag(mp3_tags, "comment");

                if (output_comments.Length > 0) mp3_tags.Comment = output_comments;

                if (opts.save_filename == true) {
                    //delete if already exists
                    remove_usertext_tag(mp3_tags, "filename");

                    //taglib doesn't support TOFN, so use TXXX instead
                    var user_filename_frame = new TagLib.Id3v2.UserTextInformationFrame("filename");
                    user_filename_frame.Text = new String[] { Path.GetFileName(opts.ModFile) };
                    mp3_tags.AddFrame(user_filename_frame);
                }

                //setting the album art to a unique image based on the tracker used
                if (opts.album_art == true) {
                    String album_art_file;
                    switch (mod_type) {
                        case "mod":
                            album_art_file = "protracker.jpg";
                            break;
                        case "s3m":
                            album_art_file = "screamtracker.jpg";
                            break;
                        case "xm":
                            album_art_file = "fasttracker.jpg";
                            break;
                        case "it":
                            album_art_file = "impulsetracker.jpg";
                            break;
                        default:
                            album_art_file = "other.jpg";
                            break;
                    }
                    //album art files should be in the executable's directory
                    album_art_file = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + album_art_file;
                    if (File.Exists(album_art_file)) {
                        try {
                            TagLib.Picture[] album_art = { new TagLib.Picture(album_art_file) };
                            album_art[0].Description = mod_type;
                            mp3_tags.Pictures = album_art;
                        } catch (Exception err) {
                            Console.WriteLine("Error loading album art: " + err.Message);
                            return 1;
                        }
                    }
                }

                try {
                    mp3file.Save();
                } catch (Exception err) {
                    Console.WriteLine("Error loading mp3 file: " + err.Message);
                    return 1;
                }

                Console.WriteLine("Wrote ID3v2." + mp3_tags.Version + " tags to input MP3 file successfully.");

                if (opts.rename == true) {
                    //start with the mod file name and replace that with the song title if there is one
                    String new_filename = Path.GetFileName(opts.ModFile);
                    if (title.Length > 0) new_filename = title;
                    if (artist.Length > 0) new_filename = new_filename.Insert(0, artist + " - ");

                    //remove illegal chars
                    Regex illegal_chars_regex = new Regex(string.Format("[{0}]", Regex.Escape(new String(Path.GetInvalidFileNameChars()))));
                    new_filename = illegal_chars_regex.Replace(new_filename, "");
                    new_filename = new_filename + Path.GetExtension(opts.MP3File);
                    String new_filepath = Path.GetDirectoryName(opts.MP3File) + Path.DirectorySeparatorChar + new_filename;

                    if (File.Exists(new_filepath)) {
                        Console.WriteLine("Could not rename mp3 file. File already exists.");
                    } else {
                        File.Move(opts.MP3File, new_filepath, false);
                        Console.WriteLine("MP3 file renamed to " + new_filename);
                    }
                }
            } else {
                //write data to stdout
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                if (title.Length > 0) Console.WriteLine("[Title] " + title);
                if (artist.Length > 0) Console.WriteLine("[Artist] " + artist);
                if (composer.Length > 0) Console.WriteLine("[Tracker] " + composer);
                if (has_date) Console.WriteLine("[Date] " + output_date.ToString("yyyy-MM-dd"));
                if (output_comments.Length > 0) Console.Write("[Comments]\n" + output_comments);
            }

            return 0;
        }

        static void remove_usertext_tag(TagLib.Id3v2.Tag mp3_tags, String name) {
            //loop through the tag object's TXXX frames and pull out the ones that match the name we want to delete
            var delete_frames = new List<TagLib.Id3v2.UserTextInformationFrame>();
            foreach (var user_frame in mp3_tags.GetFrames<TagLib.Id3v2.UserTextInformationFrame>()) {
                if (user_frame.Description == name) delete_frames.Add(user_frame);
            }
            foreach (var user_frame in delete_frames) {
                mp3_tags.RemoveFrame(user_frame);
            }
        }

        //there's no built-in marshal for utf8 strings from external dll unmanaged memory, so this function does it for us
        static String marshal_to_utf8(IntPtr in_string) {
            //using a list of bytes because we don't know the length
            var data = new List<byte>();
            var offset = 0;

            while (offset < 1048576) { //limit 1mb length
                var character = Marshal.ReadByte(in_string, offset++);
                //break on null char
                if (character == 0) {
                    break;
                }
                data.Add(character);
            }
            
            return System.Text.Encoding.UTF8.GetString(data.ToArray());
        }

        unsafe static void openmpt_log(String message, void* user) {
            Console.WriteLine("OpenMPT: " + message);
        }
    }

    public class Options {
        [Option('i', "mod", Required = true, HelpText = "Input module file path.")]
        public string ModFile { get; set; }

        [Option('o', "mp3", HelpText = "Output MP3 file path. Writes metadata to stdout if omitted.")]
        public string? MP3File { get; set; }

        [Option("id3v2", HelpText = "Version of ID3v2 to use, a value between 2 and 4. Affects compatibility with other apps. Defaults to v2.3 or the file's existing ID3 version if omitted.")]
        public byte? id3_version { get; set; }

        [Option('a', "album-art", Default = false, HelpText = "Turns on adding album art image to the MP3 file based on the tracker used.")]
        public bool album_art { get; set; }

        [Option('r', "rename", Default = false, HelpText = "Turns on renaming MP3 file based on Artist and Title.")]
        public bool rename { get; set; }

        [Option('f', "save-original-filename", Default = false, HelpText = "Copies the module's filename to a TXXX ID3 field. Useful in combination with the rename option.")]
        public bool save_filename { get; set; }

        [Option('m', "use-mod-date", Default = false, HelpText = "Falls back to using the input mod's file modified date for the ID3 date.")]
        public bool use_mod_date { get; set; }
    }
}
