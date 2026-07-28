using System.Text;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

public sealed class EncryptedCpfValueConverter : ValueConverter<Cpf, string>
{
    private const string KeyName = "cpf-criptografado-repouso"; 

    public EncryptedCpfValueConverter(IUniPlusEncryptionService encryptionService)
        : base(
            cpf => Encrypt(cpf, encryptionService),
            cipherText => Decrypt(cipherText, encryptionService))
    {
    }

    private static string Encrypt(Cpf cpf, IUniPlusEncryptionService encryption)
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(cpf.Valor);

        byte[] cipherBytes = encryption.EncryptAsync(KeyName, plaintextBytes)
                                       .GetAwaiter()
                                       .GetResult();

        return Convert.ToBase64String(cipherBytes);
    }

    private static Cpf Decrypt(string cipherText, IUniPlusEncryptionService encryption)
    {
        byte[] cipherBytes = Convert.FromBase64String(cipherText);

        byte[] plaintextBytes = encryption.DecryptAsync(KeyName, cipherBytes)
                                          .GetAwaiter()
                                          .GetResult();

        string cpfTextoLimpo = Encoding.UTF8.GetString(plaintextBytes);


        return ValueObjectMaterialization.Reidratar(Cpf.Criar(cpfTextoLimpo), nameof(Cpf));
    }
}
