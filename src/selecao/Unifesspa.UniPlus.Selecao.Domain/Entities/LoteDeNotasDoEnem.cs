namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma importação do retorno do INEP para um processo, uma etapa e uma edição do ENEM (ADR-0149).
/// Registra quem importou, quando, o hash do arquivo e o leiaute usado, e carrega os lançamentos,
/// as avaliações da etapa por oferta concorrida, os inscritos com linha e o que não virou nota. O arquivo em si não é guardado: traz nome e CPF de
/// todos os candidatos.
/// </summary>
/// <remarks>
/// Evidência append-only. A vigência não é gravada: vale o lote mais recente do processo, da etapa
/// e da edição (<see cref="VigenciaDoLote"/>), então reimportar nunca altera um lote anterior.
/// </remarks>
public sealed class LoteDeNotasDoEnem : IForensicEntity
{
    private readonly List<InscritoDoLoteDeNotas> _inscritos = [];
    private readonly List<LancamentoDeNota> _lancamentos = [];
    private readonly List<AvaliacaoDaEtapa> _avaliacoes = [];
    private readonly List<OcorrenciaDoLoteDeNotas> _ocorrencias = [];

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ProcessoSeletivoId { get; private init; }

    /// <summary>Guardado por valor, sem chave estrangeira: a etapa pertence à configuração do processo.</summary>
    public Guid EtapaProcessoId { get; private init; }

    public string EdicaoCodigo { get; private init; } = null!;

    public string LeiauteCodigo { get; private init; } = null!;

    public string LeiauteVersao { get; private init; } = null!;

    public string LeiauteHash { get; private init; } = null!;

    public string ArquivoHash { get; private init; } = null!;

    public string ImportadoPor { get; private init; } = null!;

    /// <summary>O nome de quem importou, como o token o trazia na ação.</summary>
    public string? ImportadoPorNome { get; private init; }

    public DateTimeOffset ImportadoEm { get; private init; }

    /// <summary>A versão divulgada do certame cujos pesos apuraram as avaliações do lote.</summary>
    public Guid VersaoConfiguracaoId { get; private init; }

    public int TotalDeLinhas { get; private init; }

    public int TotalDeInscritosComNota { get; private init; }

    public int TotalDeLancamentos { get; private init; }

    public int TotalDeInscritosSemLinha { get; private init; }

    public int TotalDeLinhasSemInscrito { get; private init; }

    public int TotalDeInscritosEmConflito { get; private init; }

    public IReadOnlyCollection<InscritoDoLoteDeNotas> Inscritos => _inscritos;

    public IReadOnlyCollection<LancamentoDeNota> Lancamentos => _lancamentos;

    public IReadOnlyCollection<AvaliacaoDaEtapa> Avaliacoes => _avaliacoes;

    public IReadOnlyCollection<OcorrenciaDoLoteDeNotas> Ocorrencias => _ocorrencias;

    // Construtor de materialização do EF Core.
    private LoteDeNotasDoEnem()
    {
    }

    /// <summary>
    /// Cria o lote com os lançamentos, os inscritos e as ocorrências do cruzamento, e com as
    /// avaliações apuradas. Os totais saem do cruzamento, e não de quem chama, para não poderem
    /// divergir do conteúdo; cada avaliação precisa ser de uma inscrição do cruzamento e ter a
    /// situação que o cruzamento deu a ela.
    /// </summary>
    public static LoteDeNotasDoEnem Registrar(
        Guid processoSeletivoId,
        Guid etapaProcessoId,
        string edicaoCodigo,
        string leiauteCodigo,
        string leiauteVersao,
        string leiauteHash,
        string arquivoHash,
        string importadoPor,
        DateTimeOffset importadoEm,
        int totalDeLinhas,
        Guid versaoConfiguracaoId,
        ResultadoDoCruzamento cruzamento,
        IReadOnlyList<AvaliacaoApurada> avaliacoes,
        string? importadoPorNome = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(processoSeletivoId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(etapaProcessoId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(versaoConfiguracaoId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(edicaoCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(leiauteCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(leiauteVersao);
        ArgumentException.ThrowIfNullOrWhiteSpace(importadoPor);
        ArgumentNullException.ThrowIfNull(cruzamento);
        ArgumentNullException.ThrowIfNull(avaliacoes);

        if (!HashCanonicalComputer.IsValidHashShape(leiauteHash))
        {
            throw new ArgumentException("O hash do leiaute deve ser um SHA-256 em hexadecimal minúsculo.", nameof(leiauteHash));
        }

        if (!HashCanonicalComputer.IsValidHashShape(arquivoHash))
        {
            throw new ArgumentException("O hash do arquivo deve ser um SHA-256 em hexadecimal minúsculo.", nameof(arquivoHash));
        }

        int linhasExplicadas = cruzamento.ComNota.Count
            + cruzamento.LinhasSemInscrito.Count
            + cruzamento.Conflitos.Sum(static c => c.NumerosDasLinhas.Count);
        if (totalDeLinhas < linhasExplicadas)
        {
            throw new ArgumentException("O total de linhas é menor que o das linhas que o cruzamento explica.", nameof(totalDeLinhas));
        }

        GuardarAvaliacoes(cruzamento, avaliacoes);

        LoteDeNotasDoEnem lote = new()
        {
            ProcessoSeletivoId = processoSeletivoId,
            EtapaProcessoId = etapaProcessoId,
            EdicaoCodigo = edicaoCodigo,
            LeiauteCodigo = leiauteCodigo,
            LeiauteVersao = leiauteVersao,
            LeiauteHash = leiauteHash,
            ArquivoHash = arquivoHash,
            ImportadoPor = importadoPor,
            ImportadoPorNome = string.IsNullOrWhiteSpace(importadoPorNome) ? null : importadoPorNome.Trim(),
            ImportadoEm = importadoEm,
            VersaoConfiguracaoId = versaoConfiguracaoId,
            TotalDeLinhas = totalDeLinhas,
            TotalDeInscritosComNota = cruzamento.ComNota.Count,
            TotalDeLancamentos = cruzamento.ComNota.Sum(static i => i.Areas.Count),
            TotalDeInscritosSemLinha = cruzamento.InscritosSemLinha.Count,
            TotalDeLinhasSemInscrito = cruzamento.LinhasSemInscrito.Count,
            TotalDeInscritosEmConflito = cruzamento.Conflitos.Count,
        };

        foreach (InscritoComNota inscrito in cruzamento.ComNota)
        {
            lote._inscritos.Add(InscritoDoLoteDeNotas.Registrar(lote.Id, inscrito.InscricaoId, inscrito.NumeroDeInscricaoNoEnem));
            foreach (ResultadoDaAreaNoRetorno area in inscrito.Areas)
            {
                lote._lancamentos.Add(LancamentoDeNota.Registrar(
                    lote.Id, inscrito.InscricaoId, etapaProcessoId, area.Area, area.Nota, area.Situacao));
            }
        }

        lote._avaliacoes.AddRange(avaliacoes.Select(a => AvaliacaoDaEtapa.Registrar(lote.Id, etapaProcessoId, a)));

        lote._ocorrencias.AddRange(cruzamento.InscritosSemLinha.Select(id => OcorrenciaDoLoteDeNotas.InscritoSemLinha(lote.Id, id)));
        lote._ocorrencias.AddRange(cruzamento.LinhasSemInscrito.Select(linha => OcorrenciaDoLoteDeNotas.LinhaSemInscrito(lote.Id, linha.NumeroDaLinha, linha.CpfMascarado, linha.Nome, linha.Sexo)));
        lote._ocorrencias.AddRange(cruzamento.Conflitos.Select(c => OcorrenciaDoLoteDeNotas.Conflito(lote.Id, c.InscricaoId, c.NumerosDasLinhas)));

        return lote;
    }

    private static void GuardarAvaliacoes(ResultadoDoCruzamento cruzamento, IReadOnlyList<AvaliacaoApurada> avaliacoes)
    {
        Dictionary<Guid, SituacaoDaAvaliacao> esperada = [];
        foreach (InscritoComNota inscrito in cruzamento.ComNota)
        {
            esperada[inscrito.InscricaoId] = SituacaoDaAvaliacao.Apurada;
        }

        foreach (Guid inscricaoId in cruzamento.InscritosSemLinha)
        {
            esperada[inscricaoId] = SituacaoDaAvaliacao.InscritoSemLinha;
        }

        foreach (ConflitoDeLinhas conflito in cruzamento.Conflitos)
        {
            esperada[conflito.InscricaoId] = SituacaoDaAvaliacao.EmConflito;
        }

        foreach (AvaliacaoApurada avaliacao in avaliacoes)
        {
            if (!esperada.TryGetValue(avaliacao.InscricaoId, out SituacaoDaAvaliacao situacao) || situacao != avaliacao.Situacao)
            {
                throw new ArgumentException("Toda avaliação deve ser de uma inscrição do cruzamento, com a situação que ele deu a ela.", nameof(avaliacoes));
            }
        }
    }
}
