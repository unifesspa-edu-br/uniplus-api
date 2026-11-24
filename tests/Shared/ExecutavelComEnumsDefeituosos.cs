namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Reflection;
using System.Reflection.Emit;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Um executável montado em execução com dois enums defeituosos, para a prova de que a conferência na subida impede o
/// início do host: <see cref="EnumComCodigoRepetido"/>, em que dois membros declaram o mesmo código, e
/// <see cref="EnumComCodigoAusente"/>, em que um membro não declara código.
/// </summary>
/// <remarks>
/// O assembly é emitido, e não o dos testes, para que a conferência do executável veja exatamente estes dois defeitos,
/// sem depender dos enums que outros testes declarem. Sai montado num fluxo e carregado dele, como um executável de
/// verdade, com as referências no metadado que a procura dos assemblies alcançados percorre.
/// </remarks>
internal static class ExecutavelComEnumsDefeituosos
{
    public const string EnumComCodigoRepetido = "ComCodigoRepetido";

    public const string EnumComCodigoAusente = "ComCodigoAusente";

    private static readonly ConstructorInfo ConstrutorDoAtributo = typeof(CodigoDeContratoAttribute).GetConstructor([typeof(string), typeof(string)])
        ?? throw new InvalidOperationException("O atributo não tem o construtor de código e rótulo.");

    public static Assembly Criar()
    {
        PersistedAssemblyBuilder montador = new(new AssemblyName($"Executavel.{Guid.NewGuid():N}"), typeof(object).Assembly);
        ModuleBuilder modulo = montador.DefineDynamicModule("principal");

        DefinirEnum(modulo, EnumComCodigoRepetido, ("Primeiro", "IGUAL"), ("Segundo", "IGUAL"));
        DefinirEnum(modulo, EnumComCodigoAusente, ("Declarado", "DECLARADO"), ("Sem", null));

        using MemoryStream fluxo = new();
        montador.Save(fluxo);

        return Assembly.Load(fluxo.ToArray());
    }

    // O valor numérico é a posição do membro mais um.
    private static void DefinirEnum(ModuleBuilder modulo, string nome, params (string Membro, string? Codigo)[] membros)
    {
        EnumBuilder enumeracao = modulo.DefineEnum(nome, TypeAttributes.Public, typeof(int));

        for (int posicao = 0; posicao < membros.Length; posicao++)
        {
            DeclararCodigo(enumeracao.DefineLiteral(membros[posicao].Membro, posicao + 1), membros[posicao]);
        }

        enumeracao.CreateType();
    }

    // O membro sem código não leva o atributo; o rótulo é o nome do membro.
    private static void DeclararCodigo(FieldBuilder campo, (string Membro, string? Codigo) membro)
    {
        if (membro.Codigo is { } codigo)
        {
            campo.SetCustomAttribute(new CustomAttributeBuilder(ConstrutorDoAtributo, [codigo, membro.Membro]));
        }
    }
}
