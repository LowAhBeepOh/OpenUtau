using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenUtau.Core;

namespace OpenUtau.Classic {
    public enum SymbolSetPreset { unknown, hiragana, arpabet }

    public class SymbolSet {
        public SymbolSetPreset Preset { get; set; }
        public string Head { get; set; } = "-";
        public string Tail { get; set; } = "R";
    }


    public class Subbank {
        /// <summary>
        /// Voice color, e.g., "power", "whisper". Leave unspecified for the main bank.
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Subbank prefix. Leave unspecified if none.
        /// </summary>
        public string Prefix { get; set; } = string.Empty;

        /// <summary>
        /// Subbank suffix. Leave unspecified if none.
        /// </summary>
        public string Suffix { get; set; } = string.Empty;

        /// <summary>
        /// Tone ranges. Each range specified as "C1-C4" or "C4".
        /// </summary>
        public string[] ToneRanges { get; set; }
    }

    /// <summary>
/// Fully custom piano roll colors, used when <c>voicebank_theme</c> is "custom".
/// Every field is an optional hex color ("#RRGGBB" or "#AARRGGBB");
/// unspecified colors fall back to a palette derived from the portrait.
/// </summary>
public class VoicebankThemeColors {
    public string GridBackground;
    public string GridBackgroundAlt;
    public string TickLine;
    public string TickLineLow;
    public string BarNumber;
    public string WhiteKey;
    public string WhiteKeyName;
    public string BlackKey;
    public string BlackKeyName;
    public string CenterKey;
    public string CenterKeyName;
    public string Note;
    public string NoteSelected;
    public string NoteError;
    public string Neutral;

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(GridBackground) &&
        string.IsNullOrWhiteSpace(GridBackgroundAlt) &&
        string.IsNullOrWhiteSpace(TickLine) &&
        string.IsNullOrWhiteSpace(TickLineLow) &&
        string.IsNullOrWhiteSpace(BarNumber) &&
        string.IsNullOrWhiteSpace(WhiteKey) &&
        string.IsNullOrWhiteSpace(WhiteKeyName) &&
        string.IsNullOrWhiteSpace(BlackKey) &&
        string.IsNullOrWhiteSpace(BlackKeyName) &&
        string.IsNullOrWhiteSpace(CenterKey) &&
        string.IsNullOrWhiteSpace(CenterKeyName) &&
        string.IsNullOrWhiteSpace(Note) &&
        string.IsNullOrWhiteSpace(NoteSelected) &&
        string.IsNullOrWhiteSpace(NoteError) &&
        string.IsNullOrWhiteSpace(Neutral);
}

public class VoicebankConfig {
        public string Name;
        public Dictionary<string, string> LocalizedNames;
        public string SingerType;
        public string TextFileEncoding;
        public string Image;
        public string Portrait;
        public float PortraitOpacity = 0.67f;
        public int PortraitHeight = 0;
        public string Author;
        public string Voice;
        public string Web;
        public string Version;
        public string Sample;
        public string DefaultPhonemizer;
        /// <summary>
        /// Optional piano roll theme derived from the portrait image.
        /// One of "auto", "light", "dark", "colorful", "custom".
        /// </summary>
        public string VoicebankTheme;
        /// <summary>
        /// Custom colors, used when <see cref="VoicebankTheme"/> is "custom".
        /// </summary>
        public VoicebankThemeColors VoicebankThemeColors;
        public SymbolSet SymbolSet { get; set; }
        public Subbank[] Subbanks { get; set; }
        public bool? UseFilenameAsAlias = null;

        public void Save(Stream stream) {
            using (var writer = new StreamWriter(stream, Encoding.UTF8)) {
                Yaml.DefaultSerializer.Serialize(writer, this);
            }
        }

        public static VoicebankConfig Load(Stream stream) {
            using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                var bankConfig = Yaml.DefaultDeserializer.Deserialize<VoicebankConfig>(reader);
                return bankConfig;
            }
        }
    }
}
