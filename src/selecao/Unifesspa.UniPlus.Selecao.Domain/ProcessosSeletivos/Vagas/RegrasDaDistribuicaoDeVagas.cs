namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDaDistribuicaoDeVagas
{
    internal static Result ConferirSubstituicao(
        EstadoDaConfiguracao estado,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao,
        FatosDeModalidade fatosDeModalidade)
    {
        ArgumentNullException.ThrowIfNull(estado);

        HashSet<string> novosCodigos = [.. distribuicao.SelectMany(static d => d.Modalidades).Select(static m => m.Codigo)];

        // Sem campo: o corpo da operação é só a lista de ofertas, e o campo que se corrige é o
        // número de opções, em outra operação.
        DomainError? recusa =
            DistribuicaoVazia(distribuicao)
            ?? OfertaDeCursoRepetida(distribuicao)
            ?? (HaAcaoQuandoIndeferidoDivergenteEntreOfertas(distribuicao) ? AcaoQuandoIndeferidoDivergente() : null)
            ?? RegraDeAplicabilidadeDivergenteEntreOfertas(distribuicao)
            ?? ModalidadeCitadaPorExigenciaViva(estado, fatosDeModalidade, novosCodigos)
            ?? (OpcoesDeCursoAcimaDasOfertas(estado.NumeroDeOpcoesDeCurso, distribuicao.Count)
                ? ErroDeOpcoesDeCursoAcimaDasOfertas()
                : null);
        return recusa is null ? Result.Success() : Result.Failure(recusa);
    }

    internal static Result ConferirOpcoesDeCurso(EstadoDaConfiguracao estado, int numero)
    {
        ArgumentNullException.ThrowIfNull(estado);

        if (numero is < 1 or > ProcessoSeletivo.MaximoDeOpcoesDeCurso)
        {
            return Result.ValidationFailure([new FieldError("numero", new DomainError(
                ProcessoSeletivoErrorCodes.OpcoesDeCursoInvalido,
                "O número de opções de curso deve ser 1 ou 2."))]);
        }

        return OpcoesDeCursoAcimaDasOfertas(numero, estado.DistribuicaoVagas.Count)
            ? Result.ValidationFailure([new FieldError("numero", ErroDeOpcoesDeCursoAcimaDasOfertas())])
            : Result.Success();
    }

    private static DomainError? DistribuicaoVazia(IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao) =>
        distribuicao.Count == 0
            ? new DomainError(
                "ProcessoSeletivo.DistribuicaoVagasVazia",
                "O processo deve ter ao menos uma distribuição de vagas configurada.")
            : null;

    private static DomainError? OfertaDeCursoRepetida(IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao)
    {
        List<Guid> ofertasInformadas = [.. distribuicao.Select(d => d.OfertaCursoOrigemId)];
        return ofertasInformadas.Distinct().Count() != ofertasInformadas.Count
            ? new DomainError(
                "ProcessoSeletivo.OfertaCursoDuplicada",
                "Cada oferta de curso só pode ter uma distribuição de vagas no processo.")
            : null;
    }

    // O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas
    // distintas. A restauração de envelope congelado reconstrói a distribuição fora desta escrita e
    // precisa da mesma checagem, por isso a pergunta é compartilhada.
    private static DomainError AcaoQuandoIndeferidoDivergente() => new(
        "ProcessoSeletivo.AcaoQuandoIndeferidoDivergente",
        "O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas distintas do processo.");

    // Todo fato cujos valores são modalidades referencia por código, então a checagem é precisa: só
    // recusa se um código hoje referenciado por condição viva deixaria de existir.
    private static DomainError? ModalidadeCitadaPorExigenciaViva(
        EstadoDaConfiguracao estado, FatosDeModalidade fatosDeModalidade, HashSet<string> novosCodigos) =>
        fatosDeModalidade.Codigos.Any(fato => ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(estado, fato, novosCodigos))
            ? new DomainError(
                "ProcessoSeletivo.ModalidadeReferenciadaPorExigenciaViva",
                "Existe condição de gatilho documental referenciando um código de modalidade que deixaria de ser ofertado — ajuste ou remova a condição antes de redefinir a distribuição de vagas.")
            : null;

    // Só vale com distribuição gravada: sem ela a pendência é a da distribuição ausente, e com
    // ao menos uma oferta o único número que a supera é 2.
    internal static bool OpcoesDeCursoAcimaDasOfertas(int? numeroDeOpcoes, int ofertas) =>
        numeroDeOpcoes is { } numero && ofertas > 0 && ofertas < numero;

    internal static DomainError ErroDeOpcoesDeCursoAcimaDasOfertas() => new(
        ProcessoSeletivoErrorCodes.OpcoesDeCursoAcimaDasOfertas,
        "Com 2 opções de curso, cadastre pelo menos dois cursos.");

    /// <summary>
    /// Aplicabilidade da concorrência dupla (Lei 14.723/2023, INV-B7):
    /// DERIVADA — <see langword="true"/> sse alguma modalidade selecionada em
    /// <see cref="ProcessoSeletivo.DistribuicaoVagas"/> tem
    /// <see cref="NaturezaLegalModalidade.CotaReservada"/>. Nunca um toggle
    /// livre nem um campo persistido — computada sob demanda a partir do
    /// estado corrente para nunca dessincronizar se a distribuição de vagas
    /// mudar depois de a classificação ter sido configurada.
    /// </summary>
    public static bool ConcorrenciaDuplaAplicavel(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return estado.DistribuicaoVagas
            .SelectMany(d => d.Modalidades)
            .Any(m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada);
    }

    /// <summary>
    /// Os cursos de um processo ofertam o mesmo conjunto de cotas da lei. A opção pelas cotas é do
    /// candidato no processo, e não por curso: num processo em que um curso oferta cota e outro não,
    /// quem optou pelas cotas perderia, no curso sem cota, a ação afirmativa que lá lhe caberia.
    /// </summary>
    internal static DomainError? PendenciaDasCotasDaLeiEntreOfertas(EstadoDaConfiguracao estado)
    {
        List<HashSet<string>> cotasPorOferta = [.. estado.DistribuicaoVagas.Select(static d => new HashSet<string>(
            d.Modalidades.Where(static m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada).Select(static m => m.Codigo),
            StringComparer.Ordinal))];
        return cotasPorOferta.Count > 1 && cotasPorOferta.Skip(1).Any(c => !c.SetEquals(cotasPorOferta[0]))
            ? new DomainError(
                ProcessoSeletivoErrorCodes.CotasDaLeiDiferentesEntreOfertas,
                "Os cursos do processo ofertam conjuntos diferentes de cotas da lei; todos os cursos de um processo ofertam as mesmas cotas.")
            : null;
    }

    /// <summary>
    /// A reserva de vagas para pessoa com deficiência de ampla concorrência é de quem não optou pelas
    /// cotas da lei (UNI-REQ-0142). Num processo que oferta essa reserva e alguma cota da lei, quem
    /// optou pelas cotas só tem reserva de pessoa com deficiência se o processo também ofertar a cota
    /// da lei para ela.
    /// </summary>
    internal static DomainError? PendenciaDaReservaPcdSemCotaPcdDaLei(EstadoDaConfiguracao estado)
    {
        HashSet<string> ofertadas = new(ModalidadesOfertadas(estado).Select(static m => m.Codigo), StringComparer.Ordinal);
        bool ofertaCotaDaLei = ModalidadesOfertadas(estado).Exists(static m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada);
        return ofertaCotaDaLei
            && ofertadas.Contains(ModalidadesFederaisLei12711.AcPcd)
            && !ofertadas.Contains(ModalidadesFederaisLei12711.LiPcd)
            && !ofertadas.Contains(ModalidadesFederaisLei12711.LbPcd)
            ? new DomainError(
                ProcessoSeletivoErrorCodes.ReservaPcdSemCotaPcdDaLei,
                $"O processo oferta {ModalidadesFederaisLei12711.AcPcd} e cota da lei, mas nenhuma cota da lei para pessoa com deficiência; "
                + $"oferte {ModalidadesFederaisLei12711.LiPcd} ou {ModalidadesFederaisLei12711.LbPcd}, para que quem optou pelas cotas tenha reserva.")
            : null;
    }

    /// <summary>
    /// A regra congelada tem a forma que a natureza da modalidade pede no cadastro: toda cláusula de
    /// uma cota da lei exige a opção pelas cotas, e toda cláusula de uma ação afirmativa a recusa. A
    /// cópia que não a tem não veio do cadastro como ele é hoje, e a distribuição de vagas tem de ser
    /// gravada de novo.
    /// </summary>
    internal static DomainError? RegraIncoerenteComANatureza(IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicaoVagas) =>
        distribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .FirstOrDefault(static m => m.NaturezaLegal switch
            {
                NaturezaLegalModalidade.CotaReservada => !OpcaoPelasCotasDaLei.TodaClausulaExige(m.RegraDeAplicabilidade, optou: true),
                NaturezaLegalModalidade.AcaoAfirmativa => !OpcaoPelasCotasDaLei.TodaClausulaExige(m.RegraDeAplicabilidade, optou: false),
                _ => false,
            }) is { } incoerente
            ? new DomainError(
                ProcessoSeletivoErrorCodes.RegraIncoerenteComANatureza,
                $"A regra de aplicabilidade da modalidade {incoerente.Codigo} não tem a forma que a natureza dela pede; grave a distribuição de vagas de novo para copiar a regra do cadastro.")
            : null;

    /// <summary>
    /// A cópia cujo remanejamento não é o que a natureza pede não veio do cadastro como ele é hoje:
    /// a distribuição de vagas tem de ser gravada de novo.
    /// </summary>
    internal static DomainError? RemanejamentoIncoerenteComANatureza(IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicaoVagas) =>
        distribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .FirstOrDefault(static m => !m.RemanejamentoCoerenteComANatureza()) is { } incoerente
            ? new DomainError(
                ProcessoSeletivoErrorCodes.RemanejamentoIncoerenteComANatureza,
                $"A regra de remanejamento da modalidade {incoerente.Codigo} não é a que a natureza dela pede; grave a distribuição de vagas de novo para copiar o remanejamento do cadastro.")
            : null;

    /// <summary>As modalidades ofertadas, uma por código, na ordem do código.</summary>
    internal static List<ModalidadeSelecionada> ModalidadesOfertadas(EstadoDaConfiguracao estado) =>
        [.. estado.DistribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .DistinctBy(static m => m.Codigo, StringComparer.Ordinal)
            .OrderBy(static m => m.Codigo, StringComparer.Ordinal)];

    /// <summary>
    /// Valida o grafo <b>como ele ficará</b> — as referências cruzadas são resolvidas
    /// contra as etapas do PRÓPRIO grafo, não contra as do agregado, que estão prestes
    /// a ser substituídas. Nenhuma escrita acontece antes desta função devolver
    /// <see langword="null"/>.
    /// </summary>
    internal static bool HaAcaoQuandoIndeferidoDivergenteEntreOfertas(IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicaoVagas) =>
        distribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .GroupBy(static m => m.Codigo, StringComparer.Ordinal)
            .Any(static grupo => grupo.Select(static m => m.AcaoQuandoIndeferido).Distinct().Count() > 1);

    /// <summary>
    /// A modalidade tem uma regra de aplicabilidade só no processo, porque é dela que sai quem
    /// concorre a ela (ADR-0144). Todas as distribuições são copiadas do cadastro na mesma gravação,
    /// então duas cópias diferentes da mesma modalidade só aparecem se o cadastro mudou no meio dela,
    /// ou num envelope adulterado.
    /// </summary>
    internal static DomainError? RegraDeAplicabilidadeDivergenteEntreOfertas(IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicaoVagas) =>
        distribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .GroupBy(static m => m.Codigo, StringComparer.Ordinal)
            .FirstOrDefault(static grupo => grupo.Skip(1).Any(m => !m.RegraDeAplicabilidade.MesmoConteudo(grupo.First().RegraDeAplicabilidade)))
            is { } divergente
            ? new DomainError(
                ProcessoSeletivoErrorCodes.RegraDeAplicabilidadeDivergente,
                $"A modalidade {divergente.Key} foi copiada com regras de aplicabilidade diferentes em ofertas do processo; grave a distribuição de vagas de novo.")
            : null;
}
