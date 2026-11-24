namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura das exigências documentais: declaração, modelo, prazo de emissão, validade e complementos.
/// </summary>
internal static class LeituraDasExigencias
{
    private static readonly string[] ChavesDaExigencia =
    [
        "exigenciaId", "chave", "tipoDocumentoOrigemId", "tipoDocumentoCodigo", "tipoDocumentoNome", "tipoDocumentoCategoria",
        "classificacaoProtecao", "finalidadeTratamento", "hipoteseLegal", "dadosComprovados", "exigidoNaFaseId",
        "exigidoNaEtapaId", "finalidade", "aplicabilidade", "obrigatorio", "consequenciaIndeferimento",
        "condicoes", "basesLegais", "prazoDeEmissao", "validade", "formatosPermitidos", "tamanhoMaximoBytes", "modelo",
    ];

    /// <summary>
    /// Não valida <c>exigidoNaFaseId</c> contra as fases decodificadas na mesma passagem: a checagem seria
    /// redundante, como a de <see cref="ReferenciaTemporalFatos.FaseId"/>. A resolução acontece no domínio, ao
    /// reidratar a configuração, que enxerga o mesmo Id congelado que a exigência e a política referenciam.
    /// </summary>
    internal static IReadOnlyList<DocumentoExigido> LerExigencias(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "exigencias", "documentosExigidos");
        if (leitor.Falhou)
        {
            return [];
        }

        IReadOnlyList<DocumentoExigido> exigencias = LeituraDeItens.Ler(leitor, array, "documentosExigidos.exigencias", LerExigencia) ?? [];
        ChavesNoEnvelope.ExigirUnicas(leitor, exigencias.Select(static exigencia => exigencia.Chave), "documentosExigidos.exigencias");
        return exigencias;
    }

    private static DocumentoExigido? LerExigencia(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChavesDaExigencia);

        CamposDaExigenciaNoEnvelope campos = LerCamposDaExigencia(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (!ClassificacoesDeProtecao.TryAnalisar(campos.ClassificacaoToken, out ClassificacaoDeProtecao classificacaoProtecao))
        {
            return Recusar(leitor, $"'{path}.classificacaoProtecao' fora do vocabulário de classificações de proteção.");
        }

        return LerExigenciaClassificada(leitor, item, path, campos, classificacaoProtecao);
    }

    private static DocumentoExigido? LerExigenciaClassificada(
        LeitorEnvelope leitor, JsonObject item, string path, CamposDaExigenciaNoEnvelope campos, ClassificacaoDeProtecao classificacaoProtecao)
    {
        ComplementosDaExigencia? complementos = LerComplementos(leitor, item, path);
        if (complementos is null)
        {
            return null;
        }

        FinalidadeFormulario? finalidade = EstruturaFormulario.FinalidadeDoCodigo(campos.FinalidadeToken);
        if (RecusaDaFinalidadeOuDoTamanho(campos, finalidade, path) is { } recusa)
        {
            return Recusar(leitor, recusa);
        }

        return Reidratar(campos, classificacaoProtecao, finalidade, complementos);
    }

    private static string? RecusaDaFinalidadeOuDoTamanho(CamposDaExigenciaNoEnvelope campos, FinalidadeFormulario? finalidade, string path)
    {
        if (campos.FinalidadeToken is not null && finalidade is null)
        {
            return $"'{path}.finalidade' fora do vocabulário de finalidades.";
        }

        return campos.TamanhoMaximoBytes is <= 0
            ? $"'{path}.tamanhoMaximoBytes' deve ser maior que zero quando presente."
            : null;
    }

    private static ComplementosDaExigencia? LerComplementos(LeitorEnvelope leitor, JsonObject item, string path)
    {
        IReadOnlyList<string> fatosComprovados = leitor.Textos(item, "dadosComprovados", path);
        IReadOnlyList<CondicaoGatilho> condicoes = LeituraDasCondicoes.LerCondicaoGatilho(leitor, item, path);
        IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais = LeituraDosFormatosEBasesLegais.LerBasesLegais(leitor, item, path);
        PrazoDeEmissao? prazoDeEmissao = LeituraDoPrazoEDaValidade.LerPrazo(leitor, item, path);
        ValidadeDoDocumento? validade = LeituraDoPrazoEDaValidade.LerValidade(leitor, item, path);
        FormatosPermitidos? formatosPermitidos = LeituraDosFormatosEBasesLegais.LerFormatosPermitidos(leitor, item, path);
        ModeloDaExigencia? modelo = LerModelo(leitor, item, path);

        return leitor.Falhou
            ? null
            : new ComplementosDaExigencia(fatosComprovados, condicoes, basesLegais, prazoDeEmissao, validade, formatosPermitidos!, modelo);
    }

    private static DocumentoExigido Reidratar(
        CamposDaExigenciaNoEnvelope d,
        ClassificacaoDeProtecao classificacaoProtecao,
        FinalidadeFormulario? finalidade,
        ComplementosDaExigencia c) => DocumentoExigido.Reidratar(
            d.Chave!.Value, d.ExigenciaId,
            new DocumentoExigidoDeclarado(
            new CopiaDoTipoDeDocumento(d.TipoDocumentoOrigemId, d.TipoDocumentoCodigo, d.TipoDocumentoNome, d.TipoDocumentoCategoria, new ProtecaoDoDocumento(classificacaoProtecao, d.FinalidadeTratamento, d.HipoteseLegal)),
            new MomentoDaExigencia(d.ExigidoNaFaseId, d.ExigidoNaEtapaId, finalidade),
            new AplicabilidadeDaExigencia(d.Aplicabilidade, c.Condicoes),
            new IndeferimentoDoDocumento(d.Obrigatorio, d.ConsequenciaIndeferimento, c.BasesLegais),
            new RequisitosDoDocumento(c.FormatosPermitidos, d.TamanhoMaximoBytes, c.Modelo, c.PrazoDeEmissao, c.Validade),
            c.FatosComprovados));

    private sealed record ComplementosDaExigencia(
        IReadOnlyList<string> FatosComprovados,
        IReadOnlyList<CondicaoGatilho> Condicoes,
        IReadOnlyList<DocumentoExigidoBaseLegal> BasesLegais,
        PrazoDeEmissao? PrazoDeEmissao,
        ValidadeDoDocumento? Validade,
        FormatosPermitidos FormatosPermitidos,
        ModeloDaExigencia? Modelo);

    private static DocumentoExigido? Recusar(LeitorEnvelope leitor, string mensagem) =>
        leitor.Propagar<DocumentoExigido>(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, mensagem));

    private static CamposDaExigenciaNoEnvelope LerCamposDaExigencia(LeitorEnvelope leitor, JsonObject item, string path) =>
        LerIdentidadeETipoDaExigencia(leitor, item, path) with
        {
            ExigidoNaFaseId = leitor.Identificador(item, "exigidoNaFaseId", path),
            ExigidoNaEtapaId = leitor.IdentificadorOpcional(item, "exigidoNaEtapaId", path),
            FinalidadeToken = leitor.TextoOpcional(item, "finalidade", path, LimitesDoEnvelope.Token),
            Aplicabilidade = leitor.CodigoDeContrato<Aplicabilidade>(item, "aplicabilidade", path),
            Obrigatorio = leitor.Booleano(item, "obrigatorio", path),
            ConsequenciaIndeferimento = leitor.TextoOpcional(item, "consequenciaIndeferimento", path, LimitesDoEnvelope.Token),
            TamanhoMaximoBytes = leitor.InteiroOpcional(item, "tamanhoMaximoBytes", path),
        };

    private static CamposDaExigenciaNoEnvelope LerIdentidadeETipoDaExigencia(LeitorEnvelope leitor, JsonObject item, string path) => new()
    {
        ExigenciaId = leitor.Identificador(item, "exigenciaId", path),
        Chave = ChavesNoEnvelope.Ler(leitor, item, path),
        TipoDocumentoOrigemId = leitor.Identificador(item, "tipoDocumentoOrigemId", path),
        TipoDocumentoCodigo = leitor.TextoNaoVazio(item, "tipoDocumentoCodigo", path, LimitesDoEnvelope.TipoDocumentoCodigo),
        TipoDocumentoNome = leitor.TextoNaoVazio(item, "tipoDocumentoNome", path, LimitesDoEnvelope.TipoDocumentoNome),
        TipoDocumentoCategoria = leitor.TextoNaoVazio(item, "tipoDocumentoCategoria", path, LimitesDoEnvelope.TipoDocumentoCategoria),
        ClassificacaoToken = leitor.TextoNaoVazio(item, "classificacaoProtecao", path, LimitesDoEnvelope.Token),
        FinalidadeTratamento = leitor.TextoNaoVazio(item, "finalidadeTratamento", path, LimitesDoEnvelope.FinalidadeTratamento),
        HipoteseLegal = leitor.TextoNaoVazio(item, "hipoteseLegal", path, LimitesDoEnvelope.HipoteseLegal),
    };

    private sealed record CamposDaExigenciaNoEnvelope
    {
        public Guid ExigenciaId { get; init; }

        public ChaveLocal? Chave { get; init; }

        public Guid TipoDocumentoOrigemId { get; init; }

        public string TipoDocumentoCodigo { get; init; } = string.Empty;

        public string TipoDocumentoNome { get; init; } = string.Empty;

        public string TipoDocumentoCategoria { get; init; } = string.Empty;

        public string ClassificacaoToken { get; init; } = string.Empty;

        public string FinalidadeTratamento { get; init; } = string.Empty;

        public string HipoteseLegal { get; init; } = string.Empty;

        public Guid ExigidoNaFaseId { get; init; }

        public Guid? ExigidoNaEtapaId { get; init; }

        public string? FinalidadeToken { get; init; }

        public Aplicabilidade Aplicabilidade { get; init; }

        public bool Obrigatorio { get; init; }

        public string? ConsequenciaIndeferimento { get; init; }

        public int? TamanhoMaximoBytes { get; init; }
    }

    /// <summary>
    /// O hash é SHA-256 em hexadecimal minúsculo — a forma que o encoder emite a partir de um arquivo
    /// confirmado. Vale para o modelo de documento e para o anexo do edital.
    /// </summary>
    internal static bool EhSha256Hexadecimal(string hash) =>
        hash.Length == 64 && hash.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>
    /// O modelo que a exigência oferece ao candidato; nulo quando ela não tem. O hash é o do
    /// arquivo confirmado — SHA-256 em hexadecimal minúsculo —, e o formato, um dos editáveis.
    /// </summary>
    internal static ModeloDaExigencia? LerModelo(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(item, "modelo", pathPai);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        return LerBlocoDoModelo(leitor, bloco, $"{pathPai}.modelo");
    }

    private static ModeloDaExigencia? LerBlocoDoModelo(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, "modeloId", "nomeArquivo", "formato", "hashSha256");
        ModeloNoEnvelope lido = new(
            leitor.Identificador(bloco, "modeloId", path),
            leitor.TextoNaoVazio(bloco, "nomeArquivo", path, LimitesDoEnvelope.NomeArquivoDoModelo),
            leitor.Texto(bloco, "formato", path),
            leitor.Texto(bloco, "hashSha256", path));
        return leitor.Falhou ? null : ModeloDoEnvelope(leitor, path, lido);
    }

    private static ModeloDaExigencia? ModeloDoEnvelope(LeitorEnvelope leitor, string path, ModeloNoEnvelope lido) =>
        ModeloDeDocumento.FormatoDoToken(lido.Formato) is { } formato && EhSha256Hexadecimal(lido.Hash)
            ? new ModeloDaExigencia(lido.ModeloId, lido.NomeArquivo, formato, lido.Hash)
            : leitor.Propagar<ModeloDaExigencia>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' com formato fora dos editáveis ou hash que não é SHA-256 em hexadecimal minúsculo."));

    private sealed record ModeloNoEnvelope(Guid ModeloId, string NomeArquivo, string Formato, string Hash);
}
