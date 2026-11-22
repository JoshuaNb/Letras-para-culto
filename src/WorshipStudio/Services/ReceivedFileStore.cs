using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorshipStudio.Services;

public sealed record ReceivedFile(Guid Id, string DisplayName, long Size, DateTimeOffset ReceivedAt, string Sha256);

public sealed class ReceivedFileException : IOException
{
    public int StatusCode { get; }
    public ReceivedFileException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

/// <summary>A bounded inbox. Incoming bytes are never interpreted, opened or executed.</summary>
public sealed class ReceivedFileStore
{
    private const string ReceiptName = ".worship-receipt.json";
    private readonly SemaphoreSlim _upload = new(1);
    public string DirectoryPath { get; }
    public long MaximumFileBytes { get; }
    public long MaximumStorageBytes { get; }
    public bool IsReceiving => _upload.CurrentCount == 0;

    public ReceivedFileStore(string? directory = null, long maximumFileBytes = 512L * 1024 * 1024,
        long maximumStorageBytes = 5L * 1024 * 1024 * 1024)
    {
        if (maximumFileBytes <= 0 || maximumStorageBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));
        DirectoryPath = Path.GetFullPath(directory ?? Path.Combine(LibraryStore.DataDirectoryOverride ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorshipStudio"), "received"));
        MaximumFileBytes = maximumFileBytes;
        MaximumStorageBytes = maximumStorageBytes;
    }

    public IReadOnlyList<ReceivedFile> List(int limit = 100)
    {
        if (!Directory.Exists(DirectoryPath)) return Array.Empty<ReceivedFile>();
        EnsureRegularDirectory(DirectoryPath);
        var files = new List<ReceivedFile>();
        foreach (var directory in Directory.EnumerateDirectories(DirectoryPath))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out var id) || IsReparsePoint(directory)) continue;
            var file = ReadReceipt(directory, id);
            if (file != null) files.Add(file);
        }
        return files.OrderByDescending(file => file.ReceivedAt).Take(Math.Clamp(limit, 0, 1000)).ToArray();
    }

    public string GetFilePath(Guid id)
    {
        if (!Directory.Exists(DirectoryPath)) throw new FileNotFoundException("O arquivo recebido não está mais disponível.");
        EnsureRegularDirectory(DirectoryPath);
        var file = ReadReceipt(Path.Combine(DirectoryPath, id.ToString("N")), id) ?? throw new FileNotFoundException("O arquivo recebido não está mais disponível.");
        return Path.Combine(DirectoryPath, id.ToString("N"), file.DisplayName);
    }

    private static ReceivedFile? ReadReceipt(string directory, Guid id)
    {
        try
        {
            if (!Directory.Exists(directory) || IsReparsePoint(directory)) return null;
            var receipt = Path.Combine(directory, ReceiptName);
            if (!File.Exists(receipt) || IsReparsePoint(receipt) || new FileInfo(receipt).Length > 8192) return null;
            var file = JsonSerializer.Deserialize<ReceivedFile>(File.ReadAllText(receipt));
            if (file == null || file.Id != id || file.DisplayName != ValidateName(file.DisplayName) || file.Size < 0 ||
                file.Sha256 == null || file.Sha256.Length != 64 || file.Sha256.Any(c => !Uri.IsHexDigit(c))) return null;
            var path = Path.Combine(directory, file.DisplayName);
            return File.Exists(path) && !IsReparsePoint(path) && new FileInfo(path).Length == file.Size ? file : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException) { return null; }
    }

    public async Task<ReceivedFile> ReceiveAsync(string fileName, long? declaredLength,
        Func<Stream, CancellationToken, Task> writeBody, CancellationToken ct = default)
    {
        fileName = ValidateName(fileName);
        if (declaredLength < 0) throw new ReceivedFileException(400, "O tamanho do arquivo é inválido.");
        if (declaredLength > MaximumFileBytes) throw new ReceivedFileException(413, "O arquivo excede o limite de envio permitido pelo PC.");
        if (!await _upload.WaitAsync(0, ct).ConfigureAwait(false)) throw new ReceivedFileException(429, "Outro arquivo está sendo recebido. Aguarde terminar.");
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            EnsureRegularDirectory(DirectoryPath);
            // Also serializes other app instances sharing this inbox; no data is written into the lock file.
            using var lease = AcquireLease();
            foreach (var stale in Directory.EnumerateDirectories(DirectoryPath, ".incoming-*"))
                if (Guid.TryParseExact(Path.GetFileName(stale)[".incoming-".Length..], "N", out _)) CleanupTemporary(stale);
            if (Directory.EnumerateDirectories(DirectoryPath).Count(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _)) >= 5000)
                throw new ReceivedFileException(507, "A pasta de recebidos atingiu 5.000 arquivos. Libere espaço no PC.");
            var available = MaximumStorageBytes - StoredBytes();
            if (available < 0 || declaredLength > available) throw new ReceivedFileException(507, "A pasta de recebidos atingiu o limite. Libere espaço no PC.");
            var id = Guid.NewGuid();
            temporary = Path.Combine(DirectoryPath, ".incoming-" + id.ToString("N"));
            var destination = Path.Combine(DirectoryPath, id.ToString("N"));
            Directory.CreateDirectory(temporary);
            EnsureRegularDirectory(temporary);
            var pending = Path.Combine(temporary, "content.partial");
            long size;
            string hash;
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using (var target = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var bounded = new BoundedUploadStream(target, MaximumFileBytes, available, hasher);
                    await writeBody(bounded, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    size = bounded.Length;
                    if (declaredLength.HasValue && size != declaredLength.Value) throw new ReceivedFileException(400, "A transferência foi recebida incompleta.");
                    await target.FlushAsync(ct).ConfigureAwait(false);
                }
                hash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            }
            var file = new ReceivedFile(id, fileName, size, DateTimeOffset.UtcNow, hash);
            var finalPath = Path.Combine(temporary, fileName);
            if (!pending.Equals(finalPath, StringComparison.OrdinalIgnoreCase)) File.Move(pending, finalPath);
            File.WriteAllText(Path.Combine(temporary, ReceiptName), JsonSerializer.Serialize(file), new UTF8Encoding(false));
            ct.ThrowIfCancellationRequested();
            Directory.Move(temporary, destination); // A complete receipt and file become visible together.
            temporary = null;
            return file;
        }
        catch (IOException ex) when (ex is not ReceivedFileException)
        { throw new ReceivedFileException(507, "Não foi possível guardar o arquivo. Confira o espaço e a pasta de recebidos no PC."); }
        catch (UnauthorizedAccessException)
        { throw new ReceivedFileException(507, "Não foi possível gravar na pasta de recebidos. Confira as permissões no PC."); }
        finally
        {
            if (temporary != null) CleanupTemporary(temporary);
            _upload.Release();
        }
    }

    private FileStream AcquireLease()
    {
        var path = Path.Combine(DirectoryPath, ".worship-transfer.lock");
        if (File.Exists(path) && IsReparsePoint(path)) throw new ReceivedFileException(507, "A pasta de recebidos não pode conter links de transferência.");
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None); }
        catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33)
        { throw new ReceivedFileException(429, "Outro arquivo está sendo recebido. Aguarde terminar."); }
    }

    private long StoredBytes()
    {
        long total = 0;
        foreach (var directory in Directory.EnumerateDirectories(DirectoryPath))
        {
            var name = Path.GetFileName(directory);
            if ((!Guid.TryParseExact(name, "N", out _) && !name.StartsWith(".incoming-", StringComparison.Ordinal)) || IsReparsePoint(directory)) continue;
            foreach (var file in Directory.EnumerateFiles(directory))
                if (Path.GetFileName(file) != ReceiptName && !IsReparsePoint(file)) total = checked(total + new FileInfo(file).Length);
        }
        return total;
    }

    public static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ReceivedFileException(400, "Escolha um arquivo com nome válido.");
        name = name.Normalize(NormalizationForm.FormC);
        if (name.Length > 180 || name is "." or ".." || name.EndsWith(' ') || name.EndsWith('.') ||
            name.Equals(ReceiptName, StringComparison.OrdinalIgnoreCase) || name.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)))
            throw new ReceivedFileException(400, "O nome do arquivo não é aceito no Windows. Renomeie-o no celular.");
        var stem = name.Split('.')[0].TrimEnd(' ');
        if (new[] { "CON", "PRN", "AUX", "NUL", "CLOCK$", "CONIN$", "CONOUT$" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3]))
            throw new ReceivedFileException(400, "O nome do arquivo é reservado pelo Windows. Renomeie-o no celular.");
        return name;
    }

    private static bool IsReparsePoint(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static void EnsureRegularDirectory(string path)
    {
        if (IsReparsePoint(path)) throw new ReceivedFileException(507, "A pasta de recebidos não pode ser um link ou junção.");
    }

    private void CleanupTemporary(string directory)
    {
        try
        {
            if (!Directory.Exists(directory) || IsReparsePoint(DirectoryPath) || IsReparsePoint(directory)) return;
            foreach (var file in Directory.EnumerateFiles(directory)) if (!IsReparsePoint(file)) File.Delete(file);
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private sealed class BoundedUploadStream(Stream target, long maximumBytes, long availableBytes, IncrementalHash hasher) : Stream
    {
        private long _written;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _written;
        public override long Position { get => _written; set => throw new NotSupportedException(); }
        private void Check(int count)
        {
            if (count > maximumBytes - _written) throw new ReceivedFileException(413, "O arquivo excede o limite de envio permitido pelo PC.");
            if (count > availableBytes - _written) throw new ReceivedFileException(507, "A pasta de recebidos atingiu o limite. Libere espaço no PC.");
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Check(buffer.Length);
            await target.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            hasher.AppendData(buffer.Span);
            _written += buffer.Length;
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override void Write(byte[] buffer, int offset, int count)
        { Check(count); target.Write(buffer, offset, count); hasher.AppendData(buffer, offset, count); _written += count; }
        public override void Flush() => target.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => target.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
