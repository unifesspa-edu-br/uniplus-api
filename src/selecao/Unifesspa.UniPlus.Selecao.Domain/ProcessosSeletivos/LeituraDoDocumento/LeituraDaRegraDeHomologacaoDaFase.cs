namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

/// <summary>
/// A regra de homologação das inscrições da fase, montada pelos valores do domínio a partir do que o documento copia. Não resolve o
/// motivo de decisão das condições (guarda o código) nem confere a regra contra a fase. O que os valores do domínio recusam acumula,
/// no campo de dentro da regra que o tem (<c>alternativas[i].condicoes[j].enquadramento</c>), e a regra inteira fica de fora da fase.
/// </summary>
internal static class LeituraDaRegraDeHomologacaoDaFase
{
    private const string CampoDoResultado = FaseDeclarada.CampoDaRegraDeHomologacao + ".resultadoAtoCodigo";

    internal static ValorLido<RegraDeHomologacaoDasInscricoes?> Ler(FaseNoDocumento fase)
    {
        if (fase.Publicacoes.RegraDeHomologacao is not { } declarada)
        {
            return new(null, []);
        }

        ValorLido<AlternativaDeCondicoes?>[] alternativas =
            [.. declarada.Alternativas.Select((alternativa, posicao) => Alternativa(fase, alternativa, $"{FaseDeclarada.CampoDaRegraDeHomologacao}.alternativas[{posicao}]"))];
        Result<RegraDeHomologacaoDasInscricoes> regra = RegraDeHomologacaoDasInscricoes.Criar(
            declarada.ResultadoAtoCodigo, [.. alternativas.Select(static alternativa => alternativa.Valor).OfType<AlternativaDeCondicoes>()]);
        RecusaDaParte[] recusas =
            [.. Recusas(fase, CampoDoResultado, regra.Errors), .. alternativas.SelectMany(static alternativa => alternativa.Recusas)];

        return recusas.Length > 0 ? new(null, recusas) : new(regra.Value, []);
    }

    // A causa de uma condição recusada já está nas recusas: a alternativa que a perdeu não se monta, para não recusar de novo por falta dela.
    private static ValorLido<AlternativaDeCondicoes?> Alternativa(FaseNoDocumento fase, AlternativaNoDocumento declarada, string campo)
    {
        ValorLido<CondicaoDeHomologacao?>[] condicoes =
            [.. declarada.Condicoes.Select((condicao, posicao) => Condicao(fase, condicao, $"{campo}.condicoes[{posicao}]"))];
        RecusaDaParte[] recusasDasCondicoes = [.. condicoes.SelectMany(static condicao => condicao.Recusas)];
        if (recusasDasCondicoes.Length > 0)
        {
            return new(null, recusasDasCondicoes);
        }

        Result<AlternativaDeCondicoes> alternativa = AlternativaDeCondicoes.Criar([.. condicoes.Select(static condicao => condicao.Valor!)]);

        return new(alternativa.Value, Recusas(fase, campo, alternativa.Errors));
    }

    private static ValorLido<CondicaoDeHomologacao?> Condicao(FaseNoDocumento fase, CondicaoNoDocumento declarada, string campo)
    {
        ValorLido<PredicadoDnf?> enquadramento = Enquadramento(fase, declarada.Enquadramento, $"{campo}.enquadramento");
        Result<CondicaoDeHomologacao> condicao = CondicaoDeHomologacao.Criar(
            declarada.Tipo ?? default, declarada.EdicaoDoExame, declarada.MotivoDesfavoravelCodigo, enquadramento.Valor);
        RecusaDaParte[] recusas = [.. Recusas(fase, campo, condicao.Errors), .. enquadramento.Recusas];

        return new(recusas.Length > 0 ? null : condicao.Value, recusas);
    }

    private static ValorLido<PredicadoDnf?> Enquadramento(FaseNoDocumento fase, JsonElement? declarado, string campo)
    {
        if (declarado is not { } json)
        {
            return new(null, []);
        }

        Result<PredicadoDnf> predicado = PredicadoDnfJson.DeJson(json);

        return new(predicado.Value, Recusas(fase, campo, predicado.Errors));
    }

    private static RecusaDaParte[] Recusas(FaseNoDocumento fase, string campo, IReadOnlyList<FieldError> erros) =>
        [.. erros.Select(erro => new RecusaDaParte(FaseDeclarada.CampoDaRegraDeHomologacao, campo, fase.PosicaoNoDocumento, null, erro.Error))];
}
