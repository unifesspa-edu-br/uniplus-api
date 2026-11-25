namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
    internal static ValorLido<RegraDeHomologacaoDasInscricoes?> Ler(FaseNoDocumento fase)
    {
        if (fase.Publicacoes.RegraDeHomologacao is not { } declarada)
        {
            return new(null, []);
        }

        ValorLido<AlternativaDeCondicoes?>[] alternativas =
            [.. declarada.Alternativas.Select((alternativa, posicao) => Alternativa(fase, alternativa, $"{FaseDeclarada.CampoDaRegraDeHomologacao}.alternativas[{posicao}]"))];
        RecusaDaParte[] recusas = [.. alternativas.SelectMany(static alternativa => alternativa.Recusas)];
        RegraDeHomologacaoDasInscricoes regra = RegraDeHomologacaoDasInscricoes.Declarar(
            declarada.ResultadoAtoCodigo, [.. alternativas.Select(static alternativa => alternativa.Valor).OfType<AlternativaDeCondicoes>()]);

        return recusas.Length > 0 ? new(null, recusas) : new(regra, []);
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

        Result<AlternativaDeCondicoes> alternativa = AlternativaDeCondicoes.Declarar([.. condicoes.Select(static condicao => condicao.Valor!)]);

        return new(alternativa.Value, Recusas(fase, campo, alternativa.Errors));
    }

    private static ValorLido<CondicaoDeHomologacao?> Condicao(FaseNoDocumento fase, CondicaoNoDocumento declarada, string campo)
    {
        Result<PredicadoDnf?> enquadramento = Enquadramento(declarada.Enquadramento);
        Result<CondicaoDeHomologacao> condicao = CondicaoDeHomologacao.DeclararComOEnquadramentoRecebido(
            declarada.Tipo is { } tipo && Enum.IsDefined(tipo) ? tipo.Codigo() : null, declarada.EdicaoDoExame, declarada.MotivoDesfavoravelCodigo, enquadramento);
        return new(condicao.Value, Recusas(fase, campo, condicao.Errors));
    }

    private static Result<PredicadoDnf?> Enquadramento(JsonElement? declarado)
    {
        if (declarado is not { } json)
        {
            return Result<PredicadoDnf?>.Success(null);
        }

        Result<PredicadoDnf> predicado = PredicadoDaTroca.Ler(json);
        return predicado.IsSuccess ? Result<PredicadoDnf?>.Success(predicado.Value) : Result<PredicadoDnf?>.ValidationFailure(
            [.. predicado.Errors.Select(static erro => erro with { Error = new DomainError(erro.Error.Code, MensagemDoEnquadramento(erro.Error)) })]);
    }

    private static string MensagemDoEnquadramento(DomainError erro) => erro.Code switch
    {
        PredicadoDnfJson.FormaInvalida => erro.Message,
        "CondicaoDnf.FatoObrigatorio" => "Informe o fato da condição.",
        "CondicaoDnf.OperadorInvalido" => "O operador informado não é reconhecido.",
        "CondicaoDnf.FormaIncoerenteComOperador" => "O formato do valor não corresponde ao operador informado.",
        _ => "Confira a forma do enquadramento informado.",
    };

    private static RecusaDaParte[] Recusas(FaseNoDocumento fase, string campo, IReadOnlyList<FieldError> erros) =>
        [.. erros.Select(erro => new RecusaDaParte(FaseDeclarada.CampoDaRegraDeHomologacao, erro.NoCaminhoDoItem(campo).Field!, fase.PosicaoNoDocumento, null, erro.Error))];
}
