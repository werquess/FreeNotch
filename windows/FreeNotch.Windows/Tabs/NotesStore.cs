using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeNotch;

internal sealed class NoteRecord
{
    public string Id { get; set; } = "";
    public DateTime Modified { get; set; }
    public bool Locked { get; set; }
    public string? Text { get; set; }
    public string? Salt { get; set; }
    public string? Nonce { get; set; }
    public string? Tag { get; set; }
    public string? Cipher { get; set; }
}

internal sealed class NoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Modified { get; set; } = DateTime.Now;
    public bool Locked { get; set; }
    public string Text { get; set; } = "";
    public byte[]? Key { get; set; }
    public string? Salt { get; set; }
    public string? Nonce { get; set; }
    public string? Tag { get; set; }
    public string? Cipher { get; set; }

    public bool IsReadable => !Locked || Key != null;

    public string Title
    {
        get
        {
            foreach (var line in Text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0) return trimmed;
            }
            return "";
        }
    }

    public void Forget()
    {
        if (Key != null) CryptographicOperations.ZeroMemory(Key);
        Key = null;
        if (Locked) Text = "";
    }
}

internal static class NotesCrypto
{
    private const int Iterations = 200000;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    public static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeySize);

    public static void Encrypt(NoteItem note)
    {
        if (note.Key == null) return;
        var plain = Encoding.UTF8.GetBytes(note.Text);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[plain.Length];
        using (var aes = new AesGcm(note.Key, TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }
        CryptographicOperations.ZeroMemory(plain);
        note.Nonce = Convert.ToBase64String(nonce);
        note.Tag = Convert.ToBase64String(tag);
        note.Cipher = Convert.ToBase64String(cipher);
    }

    public static bool TryDecrypt(NoteItem note, byte[] key, out string text)
    {
        text = "";
        try
        {
            var nonce = Convert.FromBase64String(note.Nonce ?? "");
            var tag = Convert.FromBase64String(note.Tag ?? "");
            var cipher = Convert.FromBase64String(note.Cipher ?? "");
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(key, TagSize))
            {
                aes.Decrypt(nonce, cipher, tag, plain);
            }
            text = Encoding.UTF8.GetString(plain);
            CryptographicOperations.ZeroMemory(plain);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

internal sealed class NotesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string path = Path.Combine(TabContext.DataFolder, "notes.json");
    private readonly object gate = new();

    public List<NoteItem> Items { get; } = new();

    public void Load()
    {
        Items.Clear();
        try
        {
            if (!File.Exists(path)) return;
            var records = JsonSerializer.Deserialize<List<NoteRecord>>(File.ReadAllText(path), JsonOptions);
            if (records == null) return;
            foreach (var record in records)
            {
                Items.Add(new NoteItem
                {
                    Id = record.Id,
                    Modified = record.Modified,
                    Locked = record.Locked,
                    Text = record.Locked ? "" : record.Text ?? "",
                    Salt = record.Salt,
                    Nonce = record.Nonce,
                    Tag = record.Tag,
                    Cipher = record.Cipher,
                });
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Save()
    {
        lock (gate)
        {
            var records = new List<NoteRecord>();
            foreach (var note in Items)
            {
                if (note.Locked && note.Key != null) NotesCrypto.Encrypt(note);
                records.Add(new NoteRecord
                {
                    Id = note.Id,
                    Modified = note.Modified,
                    Locked = note.Locked,
                    Text = note.Locked ? null : note.Text,
                    Salt = note.Locked ? note.Salt : null,
                    Nonce = note.Locked ? note.Nonce : null,
                    Tag = note.Locked ? note.Tag : null,
                    Cipher = note.Locked ? note.Cipher : null,
                });
            }
            try
            {
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(records, JsonOptions));
                File.Move(temporary, path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
