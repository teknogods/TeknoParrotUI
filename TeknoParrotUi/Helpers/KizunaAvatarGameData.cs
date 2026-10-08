using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TeknoParrotUi.Helpers
{
    /// <summary>
    /// Senjou no Kizuna's avatar pictures and greeting voices, read from the user's own game files (nothing of the game ships
    /// with TeknoParrotUI): the item table in the game's program (537 items x 3 parts {char file[16]; int layer}, found by
    /// its first file name), the parts in data_senjyo_no_kizuna_revision3_nbgi\avatar (640x360 32-bit TGA), and the
    /// greetings in sound\se\intro.nub2 (16-bit PCM). The pictures are drawn the way the game draws them
    /// (docs/protocol/40-dbaccess.md 7.7 of the Kizuna server): each item's parts on their layers 1..17, a later item
    /// replacing an earlier one on the same layer, the base body between layers 7 and 8.
    /// </summary>
    public sealed class KizunaAvatarGameData
    {
        public const string DataFolderName = "data_senjyo_no_kizuna_revision3_nbgi";
        public const int Width = 640;
        public const int Height = 360;
        public const int ItemCount = 537;
        public const int Layers = 17;

        private const string FirstItemFile = "001_00001_03";
        private const int EntrySize = 60;
        private const int PartSize = 20;

        /// <summary>The greeting's first tone in intro.nub2 (LmCall_PlayGreetingVoice: {13, 26, 52, 0}[greeting] + voice).</summary>
        private static readonly int[] GreetingTones = { 13, 26, 52, 0 };

        private static readonly Regex PartFile = new Regex(@"^\d{3}_\d{5}_\d{2}$", RegexOptions.CultureInvariant);

        private readonly Dictionary<int, (string File, int Layer)[]> _items;
        private readonly string _avatarFolder;
        private readonly string _introBank;
        private readonly Dictionary<string, byte[]> _textures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private List<(byte[] Pcm, int Channels, int Rate)> _greetings;

        private KizunaAvatarGameData(Dictionary<int, (string File, int Layer)[]> items, string avatarFolder, string introBank)
        {
            _items = items;
            _avatarFolder = avatarFolder;
            _introBank = introBank;
        }

        /// <summary>
        /// The game's avatar data next to the game's program (the profile's GamePath); null with the reason when the files are
        /// not there (the editor still works, without pictures).
        /// </summary>
        public static KizunaAvatarGameData Load(string gamePath, out string error)
        {
            error = null;
            try
            {
                var folder = string.IsNullOrEmpty(gamePath) ? null : Path.GetDirectoryName(Path.GetFullPath(gamePath));
                var avatarFolder = folder == null ? null : Path.Combine(folder, DataFolderName, "avatar");
                if (avatarFolder == null || !Directory.Exists(avatarFolder))
                {
                    error = "avatar folder not found";
                    return null;
                }

                // The table is in the station's and the terminal's program alike; the profile names the station.
                var candidates = new[] { gamePath }
                    .Concat(new[] { "n_gun_station_rel_opt_es1", "n_gun_terminal_rel_opt_es1" }.Select(name => Path.Combine(folder, name)))
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (var program in candidates)
                {
                    var items = ReadItemTable(File.ReadAllBytes(program));
                    if (items != null)
                    {
                        return new KizunaAvatarGameData(items, avatarFolder, Path.Combine(folder, DataFolderName, "sound", "se", "intro.nub2"));
                    }
                }

                error = "item table not found in the game's program";
                return null;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            {
                error = exception.Message;
                return null;
            }
        }

        /// <summary>The item table of the game's program: item id to its parts (file name, layer 1..17); null when it is not there.</summary>
        internal static Dictionary<int, (string File, int Layer)[]> ReadItemTable(byte[] program)
        {
            var start = IndexOf(program, Encoding.ASCII.GetBytes(FirstItemFile + "\0"));
            if (start < 0 || start + ItemCount * EntrySize > program.Length)
            {
                return null;
            }

            var items = new Dictionary<int, (string File, int Layer)[]>();
            for (var index = 0; index < ItemCount; index++)
            {
                var parts = new List<(string File, int Layer)>();
                for (var part = 0; part < 3; part++)
                {
                    var at = start + index * EntrySize + part * PartSize;
                    var length = Array.IndexOf(program, (byte)0, at, 16) - at;
                    var file = Encoding.ASCII.GetString(program, at, length < 0 ? 16 : length);
                    var layer = BitConverter.ToInt32(program, at + 16);
                    if (file.Length == 0)
                    {
                        continue;
                    }

                    if (!PartFile.IsMatch(file) || layer < 1 || layer > Layers)
                    {
                        return null; // not the table
                    }

                    parts.Add((file, layer));
                }

                items[index + 1] = parts.ToArray();
            }

            return items;
        }

        /// <summary>The avatar as the game draws it, for the preview.</summary>
        public BitmapSource Compose(bool female, IReadOnlyList<int> items)
        {
            var onLayer = new string[Layers + 1];
            foreach (var item in items ?? Array.Empty<int>())
            {
                if (_items.TryGetValue(item, out var parts))
                {
                    foreach (var (file, layer) in parts)
                    {
                        onLayer[layer] = file;
                    }
                }
            }

            // Premultiplied BGRA, drawn back to front.
            var canvas = new byte[Width * Height * 4];
            for (var layer = 1; layer <= Layers; layer++)
            {
                if (layer == 8)
                {
                    Draw(canvas, Texture(female ? "base_female" : "base_male"));
                }

                if (onLayer[layer] != null)
                {
                    Draw(canvas, Texture(onLayer[layer]));
                }
            }

            var bitmap = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Pbgra32, null, canvas, Width * 4);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>A greeting (0..3) in a voice (0..12) as a WAV file; null when the game's sound bank is not there.</summary>
        public byte[] GreetingWav(int greeting, int voice)
        {
            if (greeting < 0 || greeting >= GreetingTones.Length || voice < 0 || voice > 12)
            {
                return null;
            }

            _greetings = _greetings ?? ReadBank(_introBank);
            var tone = GreetingTones[greeting] + voice;
            if (_greetings == null || tone >= _greetings.Count)
            {
                return null;
            }

            var (pcm, channels, rate) = _greetings[tone];
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + pcm.Length);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(rate);
                writer.Write(rate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(pcm.Length);
                writer.Write(pcm);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>A Namco .nub2 bank of 16-bit PCM tones ("wav" entries before the data): its tones in order.</summary>
        internal static List<(byte[] Pcm, int Channels, int Rate)> ReadBank(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var bank = File.ReadAllBytes(path);
                var count = BitConverter.ToInt32(bank, 0x0C);
                var data = BitConverter.ToInt32(bank, 0x10);
                var tones = new List<(byte[] Pcm, int Channels, int Rate)>();
                var marker = Encoding.ASCII.GetBytes("wav\0");
                for (var at = IndexOf(bank, marker, 0, data); at >= 0 && tones.Count < count; at = IndexOf(bank, marker, at + 4, data))
                {
                    var size = BitConverter.ToInt32(bank, at + 0x14);
                    var offset = BitConverter.ToInt32(bank, at + 0x18);
                    var channels = BitConverter.ToInt16(bank, at + 0xBE);
                    var rate = BitConverter.ToInt32(bank, at + 0xC0);
                    if (size < 0 || offset < 0 || data + offset + size > bank.Length || channels < 1 || rate < 1000)
                    {
                        return null;
                    }

                    var pcm = new byte[size];
                    Buffer.BlockCopy(bank, data + offset, pcm, 0, size);
                    tones.Add((pcm, channels, rate));
                }

                return tones.Count == count ? tones : null;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                return null;
            }
        }

        /// <summary>A part's picture: top-down premultiplied BGRA, cached; null for a missing or broken file (drawn as nothing).</summary>
        private byte[] Texture(string file)
        {
            if (_textures.TryGetValue(file, out var cached))
            {
                return cached;
            }

            byte[] pixels = null;
            try
            {
                var path = Path.Combine(_avatarFolder, file + ".tga");
                if (File.Exists(path))
                {
                    pixels = ReadTga(File.ReadAllBytes(path));
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                pixels = null;
            }

            _textures[file] = pixels;
            return pixels;
        }

        /// <summary>An uncompressed 640x360 32-bit TGA as top-down premultiplied BGRA; null when it is anything else.</summary>
        internal static byte[] ReadTga(byte[] tga)
        {
            if (tga.Length < 18 || tga[1] != 0 || tga[2] != 2 || tga[16] != 32 ||
                BitConverter.ToUInt16(tga, 12) != Width || BitConverter.ToUInt16(tga, 14) != Height)
            {
                return null;
            }

            var start = 18 + tga[0];
            var stride = Width * 4;
            if (tga.Length < start + stride * Height)
            {
                return null;
            }

            var topDown = (tga[17] & 0x20) != 0;
            var pixels = new byte[stride * Height];
            for (var y = 0; y < Height; y++)
            {
                var from = start + (topDown ? y : Height - 1 - y) * stride;
                var to = y * stride;
                for (var x = 0; x < stride; x += 4)
                {
                    int alpha = tga[from + x + 3];
                    pixels[to + x] = (byte)(tga[from + x] * alpha / 255);
                    pixels[to + x + 1] = (byte)(tga[from + x + 1] * alpha / 255);
                    pixels[to + x + 2] = (byte)(tga[from + x + 2] * alpha / 255);
                    pixels[to + x + 3] = (byte)alpha;
                }
            }

            return pixels;
        }

        private static void Draw(byte[] canvas, byte[] layer)
        {
            if (layer == null)
            {
                return;
            }

            for (var i = 0; i < canvas.Length; i += 4)
            {
                int alpha = layer[i + 3];
                if (alpha == 0)
                {
                    continue;
                }

                var keep = 255 - alpha;
                canvas[i] = (byte)(layer[i] + canvas[i] * keep / 255);
                canvas[i + 1] = (byte)(layer[i + 1] + canvas[i + 1] * keep / 255);
                canvas[i + 2] = (byte)(layer[i + 2] + canvas[i + 2] * keep / 255);
                canvas[i + 3] = (byte)(alpha + canvas[i + 3] * keep / 255);
            }
        }

        private static int IndexOf(byte[] haystack, byte[] needle, int from = 0, int to = -1)
        {
            var end = (to < 0 ? haystack.Length : Math.Min(to, haystack.Length)) - needle.Length;
            for (var i = from; i <= end; i++)
            {
                var match = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
