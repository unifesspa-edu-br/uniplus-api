namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

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
    private static readonly Architecture ArquiteturaDoDominio = new ArchLoader().LoadAssemblies(typeof(ProcessoSeletivo).Assembly).Build();

    private static readonly Architecture ArquiteturaDoSuporteDeTeste = new ArchLoader().LoadAssemblies(typeof(TiposDeProcessoDeTeste).Assembly).Build();

    [Fact(DisplayName = "Nenhum tipo do domínio da Seleção expõe um tipo de processo fixo")]
    public void NenhumTipoDeProcessoFixoNoAssemblyDeProducao()
    {
        PropriedadesEstaticasDeTipoDeProcesso(ArquiteturaDoDominio).Should().BeEmpty(
            "os tipos de processo reais vêm do catálogo de Configuração, nunca de uma lista em código de produção");
    }

    [Fact(DisplayName = "A mesma consulta encontra os tipos fixos do suporte de teste, então a regra acima não é vácua")]
    public void ConsultaEnxergaOsTiposFixosDoSuporteDeTeste()
    {
        PropriedadesEstaticasDeTipoDeProcesso(ArquiteturaDoSuporteDeTeste).Select(nome => nome.Split('.')[^1].Split(' ')[0])
            .Should().Contain(["SiSU", "PSIQ", "PSECampo", "PSVR", "TransferenciaInterna", "TransferenciaExterna", "PortadorDiploma", "Reopcao"]);
    }

    private static IEnumerable<string> PropriedadesEstaticasDeTipoDeProcesso(Architecture arquitetura)
    {
        string tipoDoSnapshot = typeof(TipoProcessoSnapshot).FullName!;
        return PropertyMembers().GetObjects(arquitetura)
            .Where(propriedade => propriedade.IsStatic == true && propriedade.Type.FullName == tipoDoSnapshot)
            .Select(propriedade => propriedade.Name);
    }
}
