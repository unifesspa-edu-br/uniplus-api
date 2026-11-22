namespace Unifesspa.UniPlus.Selecao.ArchTests.Conferencias;

using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using Type = System.Type;

public sealed class ManifestoDoEnvelopeTests
{
    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(typeof(ConfiguracaoPublicada).Assembly, typeof(ParametrosCriterioDesempate).Assembly, typeof(CondicaoDnf).Assembly).Build();
    private static readonly HashSet<string> DaEntidadeBase = ["Id", "CreatedAt", "UpdatedAt", "DomainEvents", "IsDeleted", "DeletedAt", "DeletedBy"];
    private static readonly Type[] Unioes = [typeof(ParametrosCriterioDesempate), typeof(ParametrosRegraEliminacao), typeof(PredicadoObrigatoriedade), typeof(ParametrosRegraAjusteDistribuicao)];

    [Fact]
    public void TodaPropriedadeDeNegocio_EstaNoManifesto()
    {
        foreach ((Type tipo, (string[] congeladas, (string Propriedade, string Motivo)[] excluidas)) in ManifestoDoEnvelope.Entradas)
        {
            string[] declaradas = [.. congeladas, .. excluidas.Select(static exclusao => exclusao.Propriedade)];
            Propriedades(tipo).Select(static propriedade => propriedade.Name).Except(DaEntidadeBase)
                .Except(declaradas).Should().BeEmpty($"{tipo.Name} deve declarar cada propriedade no manifesto");
        }
    }

    [Fact]
    public void Manifesto_NaoDeclaraPropriedadeInexistente()
    {
        foreach ((Type tipo, (string[] congeladas, (string Propriedade, string Motivo)[] excluidas)) in ManifestoDoEnvelope.Entradas)
        {
            string[] declaradas = [.. congeladas, .. excluidas.Select(static exclusao => exclusao.Propriedade)];
            declaradas.Except(Propriedades(tipo).Select(static propriedade => propriedade.Name)).Should().BeEmpty(tipo.Name);
        }
    }

    [Fact]
    public void Exclusoes_TemMotivo() =>
        ManifestoDoEnvelope.Entradas.Values.SelectMany(static entrada => entrada.Excluidas)
            .Should().OnlyContain(static exclusao => !string.IsNullOrWhiteSpace(exclusao.Motivo));

    [Fact]
    public void TodoTipoDoGrafo_EstaNoManifesto()
    {
        HashSet<IType> alcancados = [];
        AlcancarPropriedades(Encontrar(typeof(ConfiguracaoPublicada)), alcancados);
        Alcancar(Encontrar(typeof(DadosEdital)), alcancados);
        Alcancar(Encontrar(typeof(ResultadoConformidade)), alcancados);
        alcancados.Should().Contain(Encontrar(typeof(DocumentoExigido)));
        alcancados.Should().Contain(Encontrar(typeof(FatoColetado)));
        alcancados.Should().Contain(Encontrar(typeof(ParametrosDesempateIdoso)));
        string[] declarados = [.. ManifestoDoEnvelope.Entradas.Keys.Select(static tipo => tipo.FullName!)];
        alcancados.Select(static tipo => tipo.FullName).Except(declarados).Should().BeEmpty();
    }

    private static IType Encontrar(Type tipo) => Types().That().Are(tipo).GetObjects(Arquitetura).SingleOrDefault()
        ?? throw new InvalidOperationException($"Tipo não encontrado: {tipo.FullName}");

    private static IEnumerable<PropertyMember> Propriedades(Type tipo) => Propriedades(Encontrar(tipo));

    private static IEnumerable<PropertyMember> Propriedades(IType tipo) =>
        PropertyMembers().That().AreDeclaredIn(tipo).And().ArePublic().GetObjects(Arquitetura)
            .Where(static propriedade => propriedade.IsStatic == false)
            .Concat(tipo is Class { BaseClass: not null } classe ? Propriedades(classe.BaseClass) : []);

    private static void AlcancarPropriedades(IType tipo, HashSet<IType> alcancados)
    {
        foreach (PropertyMember propriedade in Propriedades(tipo).Where(static propriedade => !DaEntidadeBase.Contains(propriedade.Name)))
        {
            Alcancar(propriedade.GenericArguments.FirstOrDefault()?.Type ?? propriedade.Type, alcancados);
        }
    }

    private static void Alcancar(IType tipo, HashSet<IType> alcancados)
    {
        if (!tipo.Namespace.FullName.StartsWith("Unifesspa.UniPlus.Selecao.Domain", StringComparison.Ordinal)
            || tipo is Enum || tipo.FullName == typeof(ProcessoSeletivo).FullName || !alcancados.Add(tipo))
        {
            return;
        }
        if (Unioes.Any(uniao => uniao.FullName == tipo.FullName))
        {
            alcancados.Remove(tipo);
            AlcancarVariantes(tipo, alcancados);
            return;
        }
        AlcancarPropriedades(tipo, alcancados);
    }

    private static void AlcancarVariantes(IType uniao, HashSet<IType> alcancados)
    {
        foreach (Class variante in Classes().That().AreAssignableTo(uniao).And().AreNotAbstract().GetObjects(Arquitetura))
        {
            Alcancar(variante, alcancados);
        }
    }
}
