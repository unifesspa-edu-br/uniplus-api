namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using System.Reflection;
using System.Reflection.Emit;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Enums montados em execução, para o caso de dois membros com o mesmo valor, que o C# permite mas o aviso CA1069
/// recusa na compilação e não se suprime, e para a conferência de um assembly inteiro sem contaminar o assembly dos
/// testes. Também monta o executável sem enum algum que referencia outro assembly, para provar o alcance pelas
/// referências.
/// </summary>
internal static class EnumsCriadosEmExecucao
{
    public static ModuleBuilder NovoModulo() =>
        AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("principal");

    /// <summary>Define o enum com um membro por tupla; o valor numérico é o da tupla, e não a posição.</summary>
    public static Type Definir(ModuleBuilder modulo, string nome, params (string Codigo, string Rotulo, int Valor)[] membros)
    {
        EnumBuilder enumeracao = modulo.DefineEnum(nome, TypeAttributes.Public, typeof(int));
        ConstructorInfo construtor = typeof(CodigoDeContratoAttribute).GetConstructor([typeof(string), typeof(string)])
            ?? throw new InvalidOperationException("O atributo não tem o construtor de código e rótulo.");

        for (int i = 0; i < membros.Length; i++)
        {
            FieldBuilder campo = enumeracao.DefineLiteral($"Membro{i}", membros[i].Valor);
            campo.SetCustomAttribute(new CustomAttributeBuilder(construtor, [membros[i].Codigo, membros[i].Rotulo]));
        }

        return enumeracao.CreateType();
    }

    /// <summary>
    /// Monta e carrega um assembly sem enum algum, sem o prefixo do Uni+ no nome, cuja única referência a outro assembly
    /// é a do assembly que declara o tipo recebido.
    /// </summary>
    public static Assembly ExecutavelQueReferencia(Type tipoDoAssemblyReferenciado)
    {
        PersistedAssemblyBuilder montador = new(new AssemblyName($"Executavel.{Guid.NewGuid():N}"), typeof(object).Assembly);
        TypeBuilder raiz = montador.DefineDynamicModule("principal").DefineType("Raiz", TypeAttributes.Public | TypeAttributes.Class);
        raiz.DefineField("Referencia", tipoDoAssemblyReferenciado, FieldAttributes.Public);
        raiz.CreateType();

        using MemoryStream fluxo = new();
        montador.Save(fluxo);

        return Assembly.Load(fluxo.ToArray());
    }
}
