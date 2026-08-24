namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Selecao.API.Contracts.Requests;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Os dois vocabulários fechados da configuração — fundamentos de isenção (UNI-REQ-0101) e
/// campos da divulgação pública (UNI-REQ-0050) — aparecem em três lugares: o domínio que os
/// define, a leitura que os anuncia ao cliente, e o schema do request que os declara ao
/// gerador de tipos. Um código acrescentado ao domínio e esquecido em qualquer um dos outros
/// dois é justamente o drift que esses endpoints existem para evitar.
/// </summary>
/// <remarks>
/// A comparação é sobre o conjunto de códigos, não sobre a contagem: contar acusaria a
/// omissão de um código, mas não a troca de um por outro.
/// </remarks>
public sealed class VocabulariosFechadosCompletosTests
{
    [Fact(DisplayName = "Todo fundamento de isenção do enum, exceto a sentinela, é anunciado pela leitura pública")]
    public void FundamentosDoEnum_TodosAnunciados()
    {
        string[] noDominio =
        [
            .. Enum.GetValues<FundamentoIsencao>()
                .Where(static f => f != FundamentoIsencao.Nenhum)
                .Select(static f => f.ToCodigo()),
        ];

        string[] anunciados = [.. ListarFundamentosIsencaoQueryHandler
            .Handle(new ListarFundamentosIsencaoQuery())
            .Select(static f => f.Codigo)];

        anunciados.Should().BeEquivalentTo(noDominio,
            "um fundamento que o domínio aceita e a leitura não anuncia é um fundamento que o cliente nunca oferece");
    }

    [Fact(DisplayName = "Todo fundamento anunciado tem rótulo e descrição — nenhum entra na resposta como código cru")]
    public void FundamentosAnunciados_TemRotuloEDescricao()
    {
        ListarFundamentosIsencaoQueryHandler
            .Handle(new ListarFundamentosIsencaoQuery())
            .Should().AllSatisfy(f =>
            {
                f.Nome.Should().NotBeNullOrWhiteSpace();
                f.Descricao.Should().NotBeNullOrWhiteSpace();
            });
    }

    [Fact(DisplayName = "Todo campo de divulgação declarado como constante do domínio está no vocabulário permitido")]
    public void CamposConstantes_TodosNoVocabulario()
    {
        // As constantes públicas de texto da entidade são a declaração de quais campos
        // existem; CamposPermitidos é a lista que a validação e a leitura consultam. Uma
        // constante nova que não entre na lista seria um campo que o domínio nomeia e
        // recusa ao mesmo tempo.
        string[] constantes =
        [
            .. typeof(ConfiguracaoDivulgacao)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(static f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
                .Select(static f => (string)f.GetRawConstantValue()!),
        ];

        string[] permitidos = [.. ConfiguracaoDivulgacao.CamposPermitidos.Select(static c => c.Codigo)];

        permitidos.Should().BeEquivalentTo(constantes);
    }

    [Fact(DisplayName = "Todo campo permitido pelo domínio é anunciado pela leitura pública")]
    public void CamposPermitidos_TodosAnunciados()
    {
        string[] anunciados = [.. ListarCamposDivulgacaoQueryHandler
            .Handle(new ListarCamposDivulgacaoQuery())
            .Select(static c => c.Codigo)];

        anunciados.Should().BeEquivalentTo(
            ConfiguracaoDivulgacao.CamposPermitidos.Select(static c => c.Codigo));
    }

    [Fact(DisplayName = "O schema do request de taxa declara exatamente os fundamentos que o domínio aceita")]
    public void RequestDeTaxa_DeclaraOsMesmosFundamentos()
    {
        VocabularioDeclaradoEm(typeof(DefinirTaxaInscricaoRequest), nameof(DefinirTaxaInscricaoRequest.Fundamentos))
            .Should().BeEquivalentTo(
                FundamentoIsencaoCodigo.Descritos.Select(static f => f.Codigo),
                "o schema é o que fecha o tipo gerado no cliente — um código a menos ali vira campo que o "
                + "front não sabe enviar, e um a mais vira campo que a API recusa");
    }

    [Fact(DisplayName = "O schema do request de divulgação declara exatamente os campos que o domínio permite")]
    public void RequestDeDivulgacao_DeclaraOsMesmosCampos()
    {
        VocabularioDeclaradoEm(
                typeof(DefinirConfiguracaoDivulgacaoRequest),
                nameof(DefinirConfiguracaoDivulgacaoRequest.CamposPublicos))
            .Should().BeEquivalentTo(
                ConfiguracaoDivulgacao.CamposPermitidos.Select(static c => c.Codigo));
    }

    private static IReadOnlyList<string> VocabularioDeclaradoEm(Type contrato, string propriedade)
    {
        VocabularioFechadoAttribute? atributo = contrato
            .GetProperty(propriedade)!
            .GetCustomAttribute<VocabularioFechadoAttribute>();

        atributo.Should().NotBeNull(
            $"{contrato.Name}.{propriedade} aceita um vocabulário fechado e precisa declará-lo no schema");

        return atributo!.Valores;
    }
}
