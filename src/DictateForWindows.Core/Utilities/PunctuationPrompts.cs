namespace DictateForWindows.Core.Utilities;

/// <summary>
/// Language-specific punctuation and capitalization prompts for Whisper API.
/// These prompts help the model produce properly punctuated output.
/// </summary>
public static class PunctuationPrompts
{
    /// <summary>
    /// Punctuation prompts by language code (60+ languages).
    /// </summary>
    private static readonly Dictionary<string, string> PromptsByLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "This sentence has capitalization and punctuation.",
        ["de"] = "Dieser Satz hat Großbuchstaben und Zeichensetzung.",
        ["es"] = "Esta frase tiene mayúsculas y puntuación.",
        ["fr"] = "Cette phrase contient des majuscules et de la ponctuation.",
        ["it"] = "Questa frase ha lettere maiuscole e punteggiatura.",
        ["pt"] = "Esta frase tem maiúsculas e pontuação.",
        ["nl"] = "Deze zin bevat hoofdletters en leestekens.",
        ["pl"] = "To zdanie zawiera wielkie litery i znaki interpunkcyjne.",
        ["ru"] = "В этом предложении есть заглавные буквы и знаки препинания.",
        ["ja"] = "この文には大文字と句読点があります。",
        ["ko"] = "이 문장에는 대문자와 구두점이 있습니다.",
        ["zh"] = "这句话有大写字母和标点符号。",
        ["zh-cn"] = "这句话有大写字母和标点符号。",
        ["zh-tw"] = "這句話有大寫字母和標點符號。",
        ["ar"] = "هذه الجملة تحتوي على حروف كبيرة وعلامات ترقيم.",
        ["hi"] = "इस वाक्य में कैपिटलाइज़ेशन और विराम चिह्न हैं।",
        ["bn"] = "এই বাক্যে বড় হাতের অক্ষর এবং বিরাম চিহ্ন রয়েছে।",
        ["tr"] = "Bu cümle büyük harf ve noktalama işaretleri içermektedir.",
        ["vi"] = "Câu này có viết hoa và dấu câu.",
        ["th"] = "ประโยคนี้มีตัวพิมพ์ใหญ่และเครื่องหมายวรรคตอน",
        ["cs"] = "Tato věta obsahuje velká písmena a interpunkci.",
        ["el"] = "Αυτή η πρόταση έχει κεφαλαία γράμματα και σημεία στίξης.",
        ["hu"] = "Ez a mondat nagybetűket és írásjeleket tartalmaz.",
        ["ro"] = "Această propoziție are majuscule și semne de punctuație.",
        ["sv"] = "Den här meningen innehåller versaler och skiljetecken.",
        ["da"] = "Denne sætning indeholder store bogstaver og tegnsætning.",
        ["fi"] = "Tämä lause sisältää isoja kirjaimia ja välimerkkejä.",
        ["no"] = "Denne setningen inneholder store bokstaver og tegnsetting.",
        ["nn"] = "Denne setninga inneheld store bokstavar og teiknsetjing.",
        ["uk"] = "Це речення містить великі літери та розділові знаки.",
        ["sk"] = "Táto veta obsahuje veľké písmená a interpunkciu.",
        ["bg"] = "Това изречение съдържа главни букви и пунктуация.",
        ["hr"] = "Ova rečenica sadrži velika slova i interpunkciju.",
        ["sr"] = "Ова реченица садржи велика слова и интерпункцију.",
        ["sl"] = "Ta stavek vsebuje velike črke in ločila.",
        ["lt"] = "Šiame sakinyje yra didžiosios raidės ir skyrybos ženklai.",
        ["lv"] = "Šajā teikumā ir lielie burti un pieturzīmes.",
        ["et"] = "See lause sisaldab suurtähti ja kirjavahemärke.",
        ["he"] = "במשפט זה יש אותיות גדולות וסימני פיסוק.",
        ["id"] = "Kalimat ini memiliki huruf kapital dan tanda baca.",
        ["ms"] = "Ayat ini mengandungi huruf besar dan tanda baca.",
        ["tl"] = "Ang pangungusap na ito ay may malaking titik at bantas.",
        ["sw"] = "Sentensi hii ina herufi kubwa na alama za uakifishaji.",
        ["af"] = "Hierdie sin het hoofletters en leestekens.",
        ["sq"] = "Kjo fjali ka shkronja të mëdha dhe shenja pikësimi.",
        ["hy"] = "This sentence has capitalization and punctuation.",
        ["az"] = "Bu cümlədə böyük hərflər və durğu işarələri var.",
        ["eu"] = "Esaldi honek letra larriak eta puntuazio zeinuak ditu.",
        ["be"] = "Гэты сказ мае вялікія літары і знакі прыпынку.",
        ["ka"] = "ამ წინათდებულებაში არის დიდი ასოები და სასვენი ნიშნები.",
        ["kk"] = "Бұл сөйлемде бас әріптер мен тыныс белгілері бар.",
        ["mk"] = "Оваа реченица има големи букви и интерпункција.",
        ["mr"] = "या वाक्यात कॅपिटलायझेशन आणि विरामचिन्हे आहेत.",
        ["ne"] = "यो वाक्यमा क्यापिटलाइजेशन र विराम चिह्नहरू छन्।",
        ["fa"] = "این جمله دارای حروف بزرگ و علائم نگارشی است.",
        ["pa"] = "ਇਸ ਵਾਕ ਵਿੱਚ ਕੈਪੀਟਲਾਈਜ਼ੇਸ਼ਨ ਅਤੇ ਵਿਰਾਮ ਚਿੰਨ੍ਹ ਹਨ।",
        ["ta"] = "இந்த வாக்கியத்தில் பெரிய எழுத்துக்கள் மற்றும் நிறுத்தக்குறிகள் உள்ளன.",
        ["ur"] = "اس جملے میں بڑے حروف اور رموز اوقاف ہیں۔",
        ["cy"] = "Mae gan y frawddeg hon briflythrennau ac atalnodi.",
        ["gl"] = "Esta frase ten maiúsculas e puntuación.",
        ["ca"] = "Aquesta frase té majúscules i puntuació.",
        ["yue"] = "呢句句子有大寫字母同標點符號。",
        ["yue-cn"] = "呢句句子有大写字母同标点符号。",
        ["yue-hk"] = "呢句句子有大寫字母同標點符號。",
    };

    /// <summary>
    /// Gets the punctuation prompt for a given language code.
    /// </summary>
    /// <param name="languageCode">ISO language code (e.g., "en", "de", "zh-cn").</param>
    /// <returns>The punctuation prompt for the language, or English default.</returns>
    public static string GetPromptForLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) || languageCode.Equals("detect", StringComparison.OrdinalIgnoreCase))
        {
            return PromptsByLanguage["en"];
        }

        // Try exact match first
        if (PromptsByLanguage.TryGetValue(languageCode, out var prompt))
        {
            return prompt;
        }

        // Try base language (e.g., "en-US" -> "en")
        var baseLang = languageCode.Split('-', '_')[0];
        if (PromptsByLanguage.TryGetValue(baseLang, out prompt))
        {
            return prompt;
        }

        // Default to English
        return PromptsByLanguage["en"];
    }

    /// <summary>
    /// Gets all supported language codes.
    /// </summary>
    public static IEnumerable<string> GetSupportedLanguages() => PromptsByLanguage.Keys;
}
