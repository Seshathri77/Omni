using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OmniFlow.Core;

/// <summary>
/// Service for signing and validating message signatures.
/// </summary>
public interface IMessageSigner
{
    /// <summary>
    /// Signs a message envelope.
    /// </summary>
    string Sign<T>(MessageEnvelope<T> envelope) where T : class;

    /// <summary>
    /// Validates a message signature.
    /// </summary>
    bool Validate<T>(MessageEnvelope<T> envelope) where T : class;
}

/// <summary>
/// HMAC-based message signer for secure message validation.
/// </summary>
public class HmacMessageSigner : IMessageSigner
{
    private readonly byte[] _secretKey;

    public HmacMessageSigner(string secretKey)
    {
        _secretKey = Encoding.UTF8.GetBytes(secretKey);
    }

    /// <inheritdoc/>
    public string Sign<T>(MessageEnvelope<T> envelope) where T : class
    {
        var payload = SerializeForSigning(envelope);
        using var hmac = new HMACSHA256(_secretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    /// <inheritdoc/>
    public bool Validate<T>(MessageEnvelope<T> envelope) where T : class
    {
        if (string.IsNullOrEmpty(envelope.Signature))
            return false;

        var expectedSignature = Sign(envelope);
        return envelope.Signature == expectedSignature;
    }

    private static string SerializeForSigning<T>(MessageEnvelope<T> envelope) where T : class
    {
        return JsonSerializer.Serialize(new
        {
            envelope.MessageId,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.Timestamp,
            envelope.MessageType,
            envelope.SchemaVersion,
            Message = JsonSerializer.Serialize(envelope.Message)
        });
    }
}
