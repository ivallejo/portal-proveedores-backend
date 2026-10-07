namespace WebProveedores.Application.Ports.Outbound.Files;

/// <summary>Une varios PDF en uno solo, en el orden recibido.</summary>
public interface IPdfMerger
{
    /// <summary>Lanza <see cref="InvalidDataException"/> si algún archivo no es un PDF legible.</summary>
    byte[] Merge(IReadOnlyList<byte[]> documents);
}
