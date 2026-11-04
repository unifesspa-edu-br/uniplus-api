namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
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
/// Leitura das exigências documentais: declaração, modelo, idade máxima de emissão e complementos.
/// </summary>
internal static class LeituraDasExigencias
{
    private static readonly string[] ChavesDaExigencia =
    [
        "exigenciaId", "tipoDocumentoOrigemId", "tipoDocumentoCodigo", "tipoDocumentoNome", "tipoDocumentoCategoria",
        "classificacaoProtecao", "finalidadeTratamento", "hipoteseLegal", "fatosComprovados", "exigidoNaFaseId",
        "exigidoNaEtapaId", "finalidade", "aplicabilidade", "obrigatorio", "consequenciaIndeferimento",
        "condicaoGatilho", "basesLegais", "idadeMaximaEmissao", "formatosPermitidos", "tamanhoMaximoBytes", "modelo",
    ];

    /// <summary>
    /// Não valida <c>exigidoNaFaseId</c> contra as fases decodificadas nesta mesma
    /// passagem. Desde o achado de revisão que acrescentou <c>id</c> ao bloco
    /// <c>cronogramaFases</c> (<see cref="LeituraDoCronograma.LerCronogramaFases"/>,
    /// parâmetro <c>comId</c>), <c>FaseCronograma.Id</c> é congelado e a checagem SERIA
    /// possível — mas continua redundante: a mesma razão pela qual
    /// <see cref="ReferenciaTemporalFatos.FaseId"/> (<see cref="LeituraDosFatos.LerReferenciaTemporalFatosPolitica"/>)
    /// também não é validado aqui. A resolução real acontece no domínio
    /// (<see cref="Entities.ProcessoSeletivo.RestaurarConfiguracaoCongelada"/>/<c>ResolverDataReferenciaFatos</c>),
    /// que agora enxerga o MESMO Id que a exigência/política referenciam — não um Id
    /// regenerado a cada decodificação.
    /// </summary>
    internal static IReadOnlyList<DocumentoExigido> LerExigencias(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "exigencias", "documentosExigidos");
        if (leitor.Falhou)
        {
            return [];
        }

        List<DocumentoExigido> exigencias = [];
        for (int i = 0; i < array.Count; i++)
        {
            DocumentoExigido? exigencia = LerExigencia(
                leitor,
                leitor.ItemObjeto(array, i, "documentosExigidos.exigencias"),
                $"documentosExigidos.exigencias[{i}]");
            if (exigencia is null)
            {
                return [];
            }

            exigencias.Add(exigencia);
        }

        return exigencias;
    }

    private static DocumentoExigido? LerExigencia(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChavesDaExigencia);

        DeclaracaoDaExigencia declaracao = LerDeclaracao(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (!ClassificacoesDeProtecao.TryAnalisar(declaracao.ClassificacaoToken, out ClassificacaoDeProtecao classificacaoProtecao))
        {
            return Recusar(leitor, $"'{path}.classificacaoProtecao' fora do vocabulário de classificações de proteção.");
        }

        ComplementosDaExigencia? complementos = LerComplementos(leitor, item, path);
        if (complementos is null)
        {
            return null;
        }

        FinalidadeFormulario? finalidade = declaracao.FinalidadeToken is null ? null : EstruturaFormulario.FinalidadeDoToken(declaracao.FinalidadeToken);
        if (RecusaDaFinalidadeOuDoTamanho(declaracao, finalidade, path) is { } recusa)
        {
            return Recusar(leitor, recusa);
        }

        return Reidratar(declaracao, classificacaoProtecao, finalidade, complementos);
    }

    private static string? RecusaDaFinalidadeOuDoTamanho(DeclaracaoDaExigencia declaracao, FinalidadeFormulario? finalidade, string path)
    {
        if (finalidade == FinalidadeFormulario.Nenhuma)
        {
            return $"'{path}.finalidade' fora do vocabulário de finalidades.";
        }

        return declaracao.TamanhoMaximoBytes is <= 0
            ? $"'{path}.tamanhoMaximoBytes' deve ser maior que zero quando presente."
            : null;
    }

    private static ComplementosDaExigencia? LerComplementos(LeitorEnvelope leitor, JsonObject item, string path)
    {
        IReadOnlyList<string> fatosComprovados = leitor.Textos(item, "fatosComprovados", path);
        IReadOnlyList<CondicaoGatilho> condicoes = LeituraDasCondicoes.LerCondicaoGatilho(leitor, item, path);
        IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais = LeituraDosFormatosEBasesLegais.LerBasesLegais(leitor, item, path);
        IdadeMaximaEmissao? idadeMaximaEmissao = LerIdadeMaximaEmissao(leitor, item, path);
        FormatosPermitidos? formatosPermitidos = LeituraDosFormatosEBasesLegais.LerFormatosPermitidos(leitor, item, path);
        ModeloDaExigencia? modelo = LerModelo(leitor, item, path);

        return leitor.Falhou
            ? null
            : new ComplementosDaExigencia(fatosComprovados, condicoes, basesLegais, idadeMaximaEmissao, formatosPermitidos!, modelo);
    }

    private static DocumentoExigido Reidratar(
        DeclaracaoDaExigencia d,
        ClassificacaoDeProtecao classificacaoProtecao,
        FinalidadeFormulario? finalidade,
        ComplementosDaExigencia c) => DocumentoExigido.Reidratar(
            d.ExigenciaId,
            d.ExigidoNaFaseId,
            d.TipoDocumentoOrigemId,
            d.TipoDocumentoCodigo,
            d.TipoDocumentoNome,
            d.TipoDocumentoCategoria,
            new ProtecaoDoDocumento(classificacaoProtecao, d.FinalidadeTratamento, d.HipoteseLegal),
            d.Aplicabilidade,
            d.Obrigatorio,
            d.ConsequenciaIndeferimento,
            c.Condicoes,
            c.BasesLegais,
            c.IdadeMaximaEmissao,
            c.FormatosPermitidos,
            d.TamanhoMaximoBytes,
            d.ExigidoNaEtapaId,
            c.Modelo,
            finalidade,
            c.FatosComprovados);

    private sealed record ComplementosDaExigencia(
        IReadOnlyList<string> FatosComprovados,
        IReadOnlyList<CondicaoGatilho> Condicoes,
        IReadOnlyList<DocumentoExigidoBaseLegal> BasesLegais,
        IdadeMaximaEmissao? IdadeMaximaEmissao,
        FormatosPermitidos FormatosPermitidos,
        ModeloDaExigencia? Modelo);

    private static DocumentoExigido? Recusar(LeitorEnvelope leitor, string mensagem) =>
        leitor.Propagar<DocumentoExigido>(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, mensagem));

    private static DeclaracaoDaExigencia LerDeclaracao(LeitorEnvelope leitor, JsonObject item, string path) => new()
    {
        ExigenciaId = leitor.Identificador(item, "exigenciaId", path),
        TipoDocumentoOrigemId = leitor.Identificador(item, "tipoDocumentoOrigemId", path),
        TipoDocumentoCodigo = leitor.TextoNaoVazio(item, "tipoDocumentoCodigo", path, LimitesDoEnvelope.TipoDocumentoCodigo),
        TipoDocumentoNome = leitor.TextoNaoVazio(item, "tipoDocumentoNome", path, LimitesDoEnvelope.TipoDocumentoNome),
        TipoDocumentoCategoria = leitor.TextoNaoVazio(item, "tipoDocumentoCategoria", path, LimitesDoEnvelope.TipoDocumentoCategoria),
        ClassificacaoToken = leitor.TextoNaoVazio(item, "classificacaoProtecao", path, LimitesDoEnvelope.Token),
        FinalidadeTratamento = leitor.TextoNaoVazio(item, "finalidadeTratamento", path, LimitesDoEnvelope.FinalidadeTratamento),
        HipoteseLegal = leitor.TextoNaoVazio(item, "hipoteseLegal", path, LimitesDoEnvelope.HipoteseLegal),
        ExigidoNaFaseId = leitor.Identificador(item, "exigidoNaFaseId", path),
        ExigidoNaEtapaId = leitor.IdentificadorOpcional(item, "exigidoNaEtapaId", path),
        FinalidadeToken = leitor.TextoOpcional(item, "finalidade", path, LimitesDoEnvelope.Token),
        Aplicabilidade = leitor.Enumeracao<Aplicabilidade>(item, "aplicabilidade", path),
        Obrigatorio = leitor.Booleano(item, "obrigatorio", path),
        ConsequenciaIndeferimento = leitor.TextoOpcional(item, "consequenciaIndeferimento", path, LimitesDoEnvelope.Token),
        TamanhoMaximoBytes = leitor.InteiroOpcional(item, "tamanhoMaximoBytes", path),
    };

    private sealed class DeclaracaoDaExigencia
    {
        public Guid ExigenciaId { get; init; }

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

        string path = $"{pathPai}.modelo";
        leitor.ExigirChaves(bloco, path, "modeloId", "nomeArquivo", "formato", "hashSha256");
        Guid modeloId = leitor.Identificador(bloco, "modeloId", path);
        string nomeArquivo = leitor.TextoNaoVazio(bloco, "nomeArquivo", path, LimitesDoEnvelope.NomeArquivoDoModelo);
        string formato = leitor.Texto(bloco, "formato", path);
        string hash = leitor.Texto(bloco, "hashSha256", path);
        if (leitor.Falhou)
        {
            return null;
        }

        FormatoDeDocumento? formatoDoModelo = ModeloDeDocumento.FormatoDoToken(formato);
        bool hashValido = EhSha256Hexadecimal(hash);
        return formatoDoModelo is { } formatoValido && hashValido
            ? new ModeloDaExigencia(modeloId, nomeArquivo, formatoValido, hash)
            : leitor.Propagar<ModeloDaExigencia>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' com formato fora dos editáveis ou hash que não é SHA-256 em hexadecimal minúsculo."));
    }

    internal static IdadeMaximaEmissao? LerIdadeMaximaEmissao(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonObject? idadeJson = leitor.ObjetoOpcional(item, "idadeMaximaEmissao", pathPai);
        if (leitor.Falhou || idadeJson is null)
        {
            return null;
        }

        string path = $"{pathPai}.idadeMaximaEmissao";
        leitor.ExigirChaves(idadeJson, path, "valor", "unidade", "referenciaTipo", "data", "referenciaFaseId");

        int valor = leitor.Inteiro(idadeJson, "valor", path);
        string unidadeCodigo = leitor.TextoNaoVazio(idadeJson, "unidade", path);
        string referenciaTipoCodigo = leitor.TextoNaoVazio(idadeJson, "referenciaTipo", path);
        DateOnly? data = leitor.DataOpcional(idadeJson, "data", path);
        Guid? referenciaFaseId = leitor.IdentificadorOpcional(idadeJson, "referenciaFaseId", path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (UnidadeIdadeCodigo.FromCodigo(unidadeCodigo) is not { } unidade)
        {
            return leitor.Recusar<IdadeMaximaEmissao?>($"'{path}.unidade' não reconhecida: '{unidadeCodigo}'.");
        }

        if (ReferenciaTipoIdadeEmissaoCodigo.FromCodigo(referenciaTipoCodigo) is not { } referenciaTipo)
        {
            return leitor.Recusar<IdadeMaximaEmissao?>($"'{path}.referenciaTipo' não reconhecida: '{referenciaTipoCodigo}'.");
        }

        Result<IdadeMaximaEmissao?> idadeResult = IdadeMaximaEmissao.Criar(valor, unidade, referenciaTipo, data, referenciaFaseId);
        return idadeResult.IsFailure ? leitor.Propagar<IdadeMaximaEmissao>(idadeResult.Error!) : idadeResult.Value;
    }
}
