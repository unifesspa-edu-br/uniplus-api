namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Handler do <see cref="DefinirCronogramaFasesCommand"/> (Story #851, CA-06):
/// resolve os snapshots-copy de <c>FaseCanonica</c>/<c>TipoBanca</c> (módulo
/// Configuração), o grafo de precedências vigente e o tipo do ato produzido/âncora
/// (módulo Publicações), usando a data do relógio injetado lida <b>uma vez</b> por
/// operação (ADR-0068) — e delega a montagem/validação ao domínio.
/// </summary>
public static class DefinirCronogramaFasesCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirCronogramaFasesCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFaseCanonicaReader faseCanonicaReader,
        ITipoBancaReader tipoBancaReader,
        IPrecedenciaFaseReader precedenciaFaseReader,
        IRegraCatalogoReader regraCatalogoReader,
        ITipoAtoPublicadoReader tipoAtoPublicadoReader,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(faseCanonicaReader);
        ArgumentNullException.ThrowIfNull(tipoBancaReader);
        ArgumentNullException.ThrowIfNull(precedenciaFaseReader);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(tipoAtoPublicadoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição precede a resolução de regras externas (ADR-0110 D9) — ver a
        // mesma nota nos demais Definir*.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        // UMA leitura do relógio para toda a operação (ADR-0068): a vigência do ato
        // produzido e a do ato âncora são resolvidas contra o MESMO instante.
        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        List<FaseCronograma> fases = [];
        foreach (FaseCronogramaInput input in command.Fases)
        {
            FaseCanonicaView? faseCanonica = await faseCanonicaReader
                .ObterPorIdAsync(input.FaseCanonicaId, cancellationToken)
                .ConfigureAwait(false);
            if (faseCanonica is null)
            {
                return Result<MutacaoAceita>.Failure(new DomainError(
                    "FaseCronograma.FaseCanonicaNaoEncontrada",
                    $"Fase canônica {input.FaseCanonicaId} não encontrada ou não está mais viva."));
            }

            OrigemDataFase origemData = faseCanonica.OrigemData switch
            {
                "PROPRIA" => OrigemDataFase.Propria,
                "DELEGADA" => OrigemDataFase.Delegada,
                _ => OrigemDataFase.Nenhuma,
            };

            List<BancaRequerida> bancas = [];
            foreach (Guid tipoBancaId in input.TiposBancaIds)
            {
                TipoBancaView? tipoBanca = await tipoBancaReader
                    .ObterPorIdAsync(tipoBancaId, cancellationToken)
                    .ConfigureAwait(false);
                if (tipoBanca is null)
                {
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "FaseCronograma.TipoBancaNaoEncontrado",
                        $"Tipo de banca {tipoBancaId} não encontrado ou não está mais vivo."));
                }

                bancas.Add(BancaRequerida.Criar(tipoBanca.Id, tipoBanca.Codigo));
            }

            // O ato produzido é congelado (código + irreversibilidade) para TODA fase que
            // o declara — não só para as que admitem recurso (§3.7): a irreversibilidade
            // entra na versão desde já, porque ela é imutável.
            bool atoProduzidoEfeitoIrreversivel = false;
            TipoAtoPublicadoView? tipoAtoProduzidoResolvido = null;
            if (!string.IsNullOrWhiteSpace(input.AtoProduzidoCodigo))
            {
                tipoAtoProduzidoResolvido = await tipoAtoPublicadoReader
                    .ObterVigenteAsync(input.AtoProduzidoCodigo, hoje, cancellationToken)
                    .ConfigureAwait(false);
                if (tipoAtoProduzidoResolvido is null)
                {
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "FaseCronograma.AtoProduzidoNaoEncontradoNoCatalogo",
                        $"O tipo de ato '{input.AtoProduzidoCodigo}' não tem versão vigente no catálogo de Publicações na data de hoje."));
                }

                atoProduzidoEfeitoIrreversivel = tipoAtoProduzidoResolvido.EfeitoIrreversivel;
            }

            RegraRecursoFase? regraRecurso = null;
            if (input.RegraRecurso is { } regraInput)
            {
                // CA-01/CA-02/D9: a regra referenciada é resolvida via o MESMO
                // IRegraCatalogoReader que DefinirClassificacaoCommandHandler/
                // DefinirBonusRegionalCommandHandler usam — nunca leitura própria.
                RegraCatalogo? regraCatalogo = await regraCatalogoReader
                    .ObterAsync(regraInput.RegraCodigo, regraInput.RegraVersao, cancellationToken)
                    .ConfigureAwait(false);
                if (regraCatalogo is null
                    || regraCatalogo.Tipo != TipoRegra.RegraPrazoRecurso
                    || regraCatalogo.Codigo != RegraPrazoRecursoCodigo.AncoradoEmAto)
                {
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "RegraRecursoFase.RegraCatalogoInvalida",
                        $"A regra {regraInput.RegraCodigo}/{regraInput.RegraVersao} não é a regra {RegraPrazoRecursoCodigo.AncoradoEmAto} do tipo regra_prazo_recurso."));
                }

                // A referência é montada a partir dos valores RESOLVIDOS do catálogo — nunca
                // ecoados do payload —, o que já garante por construção que o hash bate com
                // o hash declarado (o 4º item da conferência do §"Resolução da regra
                // referenciada").
                Result<ReferenciaRegra> referenciaResult = ReferenciaRegra.Criar(
                    regraCatalogo.Codigo, regraCatalogo.Versao, regraCatalogo.Hash);
                if (referenciaResult.IsFailure)
                {
                    return Result<MutacaoAceita>.Failure(referenciaResult.Error!);
                }

                ArgsRegraPrazoRecurso args = new(
                    regraInput.PrazoValor,
                    regraInput.PrazoUnidade,
                    regraInput.AtoAncoraCodigo,
                    regraInput.SuspensividadePrimeiraInstanciaValor,
                    regraInput.SuspensividadePrimeiraInstanciaUnidade,
                    regraInput.SuspensividadeSegundaInstanciaValor,
                    regraInput.SuspensividadeSegundaInstanciaUnidade);

                Result<RegraRecursoFase> regraRecursoResult = RegraRecursoFase.Criar(referenciaResult.Value!, args);
                if (regraRecursoResult.IsFailure)
                {
                    return Result<MutacaoAceita>.Failure(regraRecursoResult.Error!);
                }

                // Item 3/4 do §3.6: o ato âncora existe, está vigente e NÃO é congelante.
                // Reaproveita a resolução do ato produzido quando a âncora é o mesmo código
                // (o caso normal — o domínio já exige AtoAncoraCodigo == AtoProduzidoCodigo).
                TipoAtoPublicadoView? tipoAncora =
                    string.Equals(args.AtoAncoraCodigo, input.AtoProduzidoCodigo, StringComparison.Ordinal)
                    && tipoAtoProduzidoResolvido is not null
                        ? tipoAtoProduzidoResolvido
                        : await tipoAtoPublicadoReader
                            .ObterVigenteAsync(args.AtoAncoraCodigo, hoje, cancellationToken)
                            .ConfigureAwait(false);

                if (tipoAncora is null)
                {
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "RegraRecursoFase.AncoraNaoEncontradaNoCatalogo",
                        $"O tipo de ato âncora '{args.AtoAncoraCodigo}' não tem versão vigente no catálogo de Publicações na data de hoje."));
                }

                if (tipoAncora.CongelaConfiguracao)
                {
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "RegraRecursoFase.AncoraEmAtoCongelante",
                        $"O tipo de ato âncora '{args.AtoAncoraCodigo}' congela configuração — a âncora do recurso nunca é o ato que congela a configuração."));
                }

                regraRecurso = regraRecursoResult.Value!;
            }

            Result<FaseCronograma> faseResult = FaseCronograma.Criar(
                input.Ordem,
                faseCanonica.Id,
                faseCanonica.Codigo,
                faseCanonica.DonoTipico,
                origemData,
                faseCanonica.AgrupaEtapas,
                faseCanonica.PermiteComplementacao,
                faseCanonica.ProduzResultado,
                faseCanonica.ResultadoDefinitivo,
                faseCanonica.ColetaInscricao,
                input.Inicio,
                input.Fim,
                input.AtoProduzidoCodigo,
                atoProduzidoEfeitoIrreversivel,
                bancas,
                regraRecurso);
            if (faseResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(faseResult.Error!);
            }

            fases.Add(faseResult.Value!);
        }

        // §3.3: o grafo de precedências é parâmetro, não navegação (ADR-0042) — resolvido
        // aqui e passado pronto ao domínio.
        IReadOnlyList<PrecedenciaFaseView> arestasVivas = await precedenciaFaseReader
            .ListarVivasAsync(cancellationToken)
            .ConfigureAwait(false);
        List<ArestaPrecedencia> precedencias = [.. arestasVivas
            .Select(static a => new ArestaPrecedencia(a.AntecessoraCodigo, a.SucessoraCodigo, a.PermiteSobreposicao))];

        Result definirResult = processo.DefinirCronogramaFases(fases, precedencias, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
