namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Validação e normalização dos campos editáveis de <see cref="Modalidade"/>: tokens dos enums, tamanhos e
/// coerência entre campos, acumulando toda violação em vez de parar na primeira. Sem I/O.
/// </summary>
public static class ValidacaoDosCamposDaModalidade
{
    private const int DescricaoMaxLength = 300;
    private const int BaseLegalMaxLength = 500;
    private const int CodigoReferenciaMaxLength = 60;

    /// <summary>
    /// Valida só os campos independentes de coerência cruzada (parse dos enums,
    /// tamanhos), sem I/O — para o handler de atualização falhar rápido antes do
    /// fetch. <b>Não</b> inclui as três invariantes cruzadas nem a guarda do
    /// catálogo legal fixo: a guarda depende do <c>Codigo</c> já persistido, e as
    /// coerências têm de vir depois dela — reportá-las aqui, antes do fetch,
    /// preemptiria <c>EstruturaProtegidaNaoEditavel</c> com um erro de coerência
    /// para um payload que também é internamente incoerente. Só
    /// <see cref="Atualizar"/>, depois do fetch, avalia guarda e coerência na
    /// ordem certa.
    /// </summary>
    public static Result ValidarCamposDoPayload(
        string? descricao,
        string? naturezaLegalToken,
        string? composicaoVagasToken,
        string? composicaoOrigem,
        string? regraRemanejamentoToken,
        string? remanejamentoDestino,
        string? remanejamentoPar,
        string? remanejamentoFallback,
        PredicadoDnf? regraDeAplicabilidade,
        string? acaoQuandoIndeferidoToken,
        string? baseLegal)
    {
        (List<FieldError> erros, _, _, _, _) = ValidarCamposIndependentes(
            descricao, naturezaLegalToken, composicaoVagasToken, composicaoOrigem, regraRemanejamentoToken,
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
            regraDeAplicabilidade, acaoQuandoIndeferidoToken, baseLegal);

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Valida os campos editáveis por completo, acumulando toda violação
    /// independente — os campos que não cruzam entre si (<see cref="ValidarCamposIndependentes"/>)
    /// e as três invariantes cruzadas (RetiraDe⟺origem, natureza↔regra, args por
    /// regra), cada uma só avaliada quando os tokens de que ela depende já foram
    /// reconhecidos (senão o erro reportado seria derivado de um token inválido,
    /// não uma incoerência independente — ex.: reportar "SEGUE_CASCATA exigido"
    /// sobre uma <c>NaturezaLegal</c> que nem chegou a resolver para um valor de
    /// domínio). Sem I/O — existe também para o handler de atualização falhar
    /// rápido antes de buscar a modalidade por Id.
    /// </summary>
    public static Result<CamposResolvidosDaModalidade> ValidarCampos(
        string? descricao,
        string? naturezaLegalToken,
        string? composicaoVagasToken,
        string? composicaoOrigem,
        string? regraRemanejamentoToken,
        string? remanejamentoDestino,
        string? remanejamentoPar,
        string? remanejamentoFallback,
        PredicadoDnf? regraDeAplicabilidade,
        string? acaoQuandoIndeferidoToken,
        string? baseLegal,
        CodigoModalidade? codigo = null)
    {
        (List<FieldError> erros, bool naturezaOk, bool composicaoOk, bool regraOk, CamposResolvidosDaModalidade campos) =
            ValidarCamposIndependentes(
                descricao, naturezaLegalToken, composicaoVagasToken, composicaoOrigem, regraRemanejamentoToken,
                remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
                regraDeAplicabilidade, acaoQuandoIndeferidoToken, baseLegal);

        erros.AddRange(CoerenciaDosCamposDaModalidade.ValidarCoerenciaCruzada(campos, composicaoOk, naturezaOk, regraOk));

        if (codigo is not null)
        {
            erros.AddRange(CoerenciaDosCamposDaModalidade.ValidarAutorreferencia(codigo, campos));
        }

        return erros.Count == 0
            ? Result<CamposResolvidosDaModalidade>.Success(campos)
            : Result<CamposResolvidosDaModalidade>.ValidationFailure(erros);
    }

    /// <summary>
    /// Valida e normaliza os campos editáveis independentes de qualquer coerência
    /// cruzada, acumulando toda violação em vez de parar na primeira — tokens dos
    /// enums, normalização de texto e tamanhos. Também devolve, junto do lote de
    /// erros, se cada token do qual alguma invariante cruzada depende foi
    /// reconhecido (<paramref name="naturezaLegalToken"/> → natureza,
    /// <paramref name="composicaoVagasToken"/> → composição,
    /// <paramref name="regraRemanejamentoToken"/> → regra): quando o lote de erros
    /// está vazio, os três sinalizadores são sempre verdadeiros, por construção.
    /// Sem I/O — separada de <see cref="ValidarCoerenciaCruzada"/> porque
    /// <see cref="Atualizar"/> precisa intercalar a guarda do catálogo legal fixo
    /// entre as duas.
    /// </summary>
    internal static (List<FieldError> Erros, bool NaturezaOk, bool ComposicaoOk, bool RegraOk, CamposResolvidosDaModalidade Campos)
        ValidarCamposIndependentes(
        string? descricao,
        string? naturezaLegalToken,
        string? composicaoVagasToken,
        string? composicaoOrigem,
        string? regraRemanejamentoToken,
        string? remanejamentoDestino,
        string? remanejamentoPar,
        string? remanejamentoFallback,
        PredicadoDnf? regraDeAplicabilidade,
        string? acaoQuandoIndeferidoToken,
        string? baseLegal)
    {
        List<FieldError> erros = [];

        string? descricaoNorm = NormalizarOpcional(descricao);
        if (descricaoNorm is not null && descricaoNorm.Length > DescricaoMaxLength)
        {
            erros.Add(new("descricao", new DomainError(
                ModalidadeErrorCodes.DescricaoTamanho,
                $"Descrição da modalidade deve ter no máximo {DescricaoMaxLength} caracteres.")));
        }

        string? baseLegalNorm = NormalizarOpcional(baseLegal);
        if (baseLegalNorm is not null && baseLegalNorm.Length > BaseLegalMaxLength)
        {
            erros.Add(new("baseLegal", new DomainError(
                ModalidadeErrorCodes.BaseLegalTamanho,
                $"Base legal da modalidade deve ter no máximo {BaseLegalMaxLength} caracteres.")));
        }

        // NaturezaLegal — obrigatória, default AMPLA quando ausente.
        bool naturezaOk = true;
        NaturezaLegal natureza = NaturezaLegal.Ampla;
        if (!string.IsNullOrWhiteSpace(naturezaLegalToken) && !NaturezasLegais.TryAnalisar(naturezaLegalToken, out natureza))
        {
            naturezaOk = false;
            erros.Add(new("naturezaLegal", new DomainError(
                ModalidadeErrorCodes.NaturezaInvalida,
                $"Natureza legal deve ser uma de: {string.Join(", ", NaturezasLegais.TokensCanonicos)}.")));
        }

        // ComposicaoVagas — obrigatória, default RESIDUAL_DO_VO quando ausente.
        bool composicaoOk = true;
        ComposicaoVagas composicao = ComposicaoVagas.ResidualDoVo;
        if (!string.IsNullOrWhiteSpace(composicaoVagasToken) && !ComposicoesVagas.TryAnalisar(composicaoVagasToken, out composicao))
        {
            composicaoOk = false;
            erros.Add(new("composicaoVagas", new DomainError(
                ModalidadeErrorCodes.ComposicaoVagasInvalida,
                $"Composição de vagas deve ser uma de: {string.Join(", ", ComposicoesVagas.TokensCanonicos)}.")));
        }

        // RegraRemanejamento — opcional (null quando ausente).
        bool regraOk = true;
        RegraRemanejamento? regra = null;
        if (!string.IsNullOrWhiteSpace(regraRemanejamentoToken))
        {
            if (RegrasRemanejamento.TryAnalisar(regraRemanejamentoToken, out RegraRemanejamento regraResolvida))
            {
                regra = regraResolvida;
            }
            else
            {
                regraOk = false;
                erros.Add(new("regraRemanejamento", new DomainError(
                    ModalidadeErrorCodes.RegraRemanejamentoInvalida,
                    $"Regra de remanejamento deve ser uma de: {string.Join(", ", RegrasRemanejamento.TokensCanonicos)}.")));
            }
        }

        // AcaoQuandoIndeferido — opcional (null quando ausente); quando informada, deve ser
        // um dos dois tokens (invariante 6). Nenhuma invariante deste agregado depende dela
        // — sem gate. A ausência não é lacuna: declara que a modalidade não reclassifica, e
        // o destino do candidato indeferido passa a ser o que a exigência documental que o
        // alcançou declarar (consequência de indeferimento, em Seleção).
        AcaoQuandoIndeferido? acao = null;
        if (!string.IsNullOrWhiteSpace(acaoQuandoIndeferidoToken))
        {
            if (AcoesQuandoIndeferido.TryAnalisar(acaoQuandoIndeferidoToken, out AcaoQuandoIndeferido acaoResolvida))
            {
                acao = acaoResolvida;
            }
            else
            {
                erros.Add(new("acaoQuandoIndeferido", new DomainError(
                    ModalidadeErrorCodes.AcaoIndeferimentoInvalida,
                    $"Ação quando indeferido deve ser uma de: {string.Join(", ", AcoesQuandoIndeferido.TokensCanonicos)}.")));
            }
        }

        string? origemNorm = NormalizarOpcional(composicaoOrigem);
        if (origemNorm is not null && origemNorm.Length > CodigoReferenciaMaxLength)
        {
            erros.Add(new("composicaoOrigem", new DomainError(
                ModalidadeErrorCodes.CodigoFormatoInvalido,
                $"Código de origem da composição deve ter no máximo {CodigoReferenciaMaxLength} caracteres.")));
        }

        RemanejamentoArgs args = RemanejamentoArgs.Criar(
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback);

        if (regraDeAplicabilidade is null)
        {
            erros.Add(new("regraDeAplicabilidade", new DomainError(
                ModalidadeErrorCodes.RegraDeAplicabilidadeObrigatoria,
                "A modalidade precisa da regra que diz quem concorre a ela; a ampla concorrência a declara sem condição.")));
        }

        var campos = new CamposResolvidosDaModalidade(
            descricaoNorm, natureza, composicao, origemNorm, regra, args, regraDeAplicabilidade, acao, baseLegalNorm);

        return (erros, naturezaOk, composicaoOk, regraOk, campos);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
