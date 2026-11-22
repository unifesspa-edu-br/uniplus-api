namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// O tipo de processo é configuração: vem do catálogo do módulo Configuração. O assembly de produção do
/// domínio não pode carregar um conjunto fixo de tipos, o que sugeriria um conjunto fechado.
/// </summary>
public sealed class TiposDeProcessoSoComoConfiguracaoTests
{
    private static readonly Architecture ArquiteturaDoDominio = ArquiteturaDaSelecao.Produto;

    private static readonly Architecture ArquiteturaDoSuporteDeTeste = ArquiteturaDaSelecao.Produto;

    [Fact(DisplayName = "Nenhum tipo do domínio da Seleção expõe um tipo de processo fixo")]
    public void NenhumTipoDeProcessoFixoNoAssemblyDeProducao()
    {
        PropriedadesEstaticasDeTipoDeProcesso(ArquiteturaDoDominio, "Unifesspa.UniPlus.Selecao.Domain").Should().BeEmpty(
            "os tipos de processo reais vêm do catálogo de Configuração, nunca de uma lista em código de produção");
    }

    [Fact(DisplayName = "A mesma consulta encontra os tipos fixos do suporte de teste, então a regra acima não é vácua")]
    public void ConsultaEnxergaOsTiposFixosDoSuporteDeTeste()
    {
        PropriedadesEstaticasDeTipoDeProcesso(ArquiteturaDoSuporteDeTeste, "Unifesspa.UniPlus.Testes.Compartilhado").Select(nome => nome.Split('.')[^1].Split(' ')[0])
            .Should().Contain(["SiSU", "PSIQ", "PSECampo", "PSVR", "TransferenciaInterna", "TransferenciaExterna", "PortadorDiploma", "Reopcao"]);
    }

    private static IEnumerable<string> PropriedadesEstaticasDeTipoDeProcesso(Architecture arquitetura, string escopo)
    {
        string tipoDoSnapshot = typeof(TipoProcessoSnapshot).FullName!;
        return PropertyMembers().That().AreDeclaredIn(Types().That().ResideInNamespaceMatching("^" + escopo.Replace(".", @"\.", StringComparison.Ordinal))).GetObjects(arquitetura)
            .Where(propriedade => propriedade.IsStatic == true && propriedade.Type.FullName == tipoDoSnapshot)
            .Select(propriedade => propriedade.Name);
    }
}
