namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Salva o rascunho (issue #1859) sobre a versão do formulário fixada ao iniciar — nunca a vigente
/// por relógio. A validação é estrutural: só recusa por campo a resposta que viola uma restrição do
/// formulário; nunca por obrigatoriedade, que é exigida somente na submissão.
/// </summary>
public static class SalvarInscricaoCommandHandler
{
    public static async Task<Result<InscricaoDto>> Handle(
        SalvarInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<InscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode salvar a inscrição."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<InscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterPorProcessoECpfAsync(command.ProcessoSeletivoId, cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<InscricaoDto>.Failure(new DomainError(
                InscricaoErrorCodes.NaoEncontrada,
                $"Nenhuma inscrição de {cpfDoToken} no processo seletivo {command.ProcessoSeletivoId}."));
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<InscricaoDto>.Failure(erroDono);
        }

        Result<FormularioPortavel> regras = await ResolverRegrasDaVersaoFixadaAsync(
            inscricao.VersaoFormularioId, command.ProcessoSeletivoId, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (regras.IsFailure)
        {
            return Result<InscricaoDto>.Failure(regras.Error!);
        }

        Dictionary<string, JsonElement> respostas = command.Request.Respostas is { } r
            ? new(r, StringComparer.Ordinal)
            : new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = ParaDominio(command.Request.Grupos);

        Result<AvaliacaoFormulario> avaliacao = regras.Value!.Avaliar(new EntradaAvaliacaoFormulario(
            respostas,
            EtapasConcluidas: new HashSet<string>(StringComparer.Ordinal),
            FatosConhecidos: new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            RespostasDosGrupos: ParaAvaliacao(grupos)));
        if (avaliacao.IsFailure)
        {
            return Result<InscricaoDto>.Failure(avaliacao.Error!);
        }

        IReadOnlyList<FieldError> recusas = RecusasEstruturais(avaliacao.Value!);
        if (recusas.Count > 0)
        {
            return Result<InscricaoDto>.ValidationFailure(recusas);
        }

        List<AceiteDeTermo> aceites = [.. (command.Request.Aceites ?? []).Select(
            static a => new AceiteDeTermo(a.Codigo, a.VersaoId, a.HashVersao))];

        Result salvar = inscricao.Salvar(command.Request.OfertaCursoId, respostas, grupos, aceites, command.Precondicao);
        if (salvar.IsFailure)
        {
            return Result<InscricaoDto>.Failure(salvar.Error!);
        }

        await inscricaoRepository.AtualizarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<InscricaoDto>.Success(InscricaoDto.De(inscricao));
    }

    /// <summary>
    /// As regras da finalidade Inscrição, recortadas da versão FIXADA pela inscrição ao iniciar —
    /// nunca a vigente por relógio: uma retificação publicada depois não muda a validação de uma
    /// inscrição já em curso (UNI-REQ-0084).
    /// </summary>
    private static async Task<Result<FormularioPortavel>> ResolverRegrasDaVersaoFixadaAsync(
        Guid versaoFormularioId,
        Guid processoSeletivoId,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        Result<VersaoConfiguracao> resolvida = await Services.VersaoFixadaDaInscricaoResolver
            .ResolverAsync(versaoFormularioId, processoSeletivoId, processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (resolvida.IsFailure)
        {
            return Result<FormularioPortavel>.Failure(resolvida.Error!);
        }

        VersaoConfiguracao versao = resolvida.Value!;

        if (!registroCodecs.SabeLer(versao.SchemaVersion))
        {
            return Result<FormularioPortavel>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                $"A versão '{versao.SchemaVersion}' do envelope congelado não está entre as capacidades de leitura reconhecidas pelo codec vivo."));
        }

        Result<EnvelopeReidratado> reidratado = registroCodecs.Reidratar(versao);
        if (reidratado.IsFailure)
        {
            return Result<FormularioPortavel>.Failure(reidratado.Error!);
        }

        Result<Services.DefinicaoAvaliavel> avaliavel = Services.DefinicaoAvaliavelDoProcesso.DoCongelado(reidratado.Value!);
        if (avaliavel.IsFailure)
        {
            return Result<FormularioPortavel>.Failure(avaliavel.Error!);
        }

        RecorteDaFinalidade recorte = RecorteDaFinalidade.De(
            avaliavel.Value!.Definicao,
            Domain.Services.DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, null),
            avaliavel.Value!.Ofertas);
        return Result<FormularioPortavel>.Success(recorte.Regras);
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> ParaDominio(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? grupos)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> resultado = new(StringComparer.Ordinal);
        if (grupos is null)
        {
            return resultado;
        }

        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput> ocorrencias) in grupos)
        {
            resultado[codigo] = [.. ocorrencias.Select(static o => new OcorrenciaDeGrupoInscricao(
                o.Id, o.Respostas is { } respostas ? new Dictionary<string, JsonElement>(respostas, StringComparer.Ordinal) : new(StringComparer.Ordinal)))];
        }

        return resultado;
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ParaAvaliacao(
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> resultado = new(StringComparer.Ordinal);
        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in grupos)
        {
            resultado[codigo] = [.. ocorrencias.Select(static o => new OcorrenciaRespondida(o.Id, o.Respostas))];
        }

        return resultado;
    }

    /// <summary>
    /// Só a resposta que viola uma restrição do formulário é recusada — a ausência de resposta a um
    /// item obrigatório não é, porque o rascunho não exige obrigatórios. Cobre os itens de nível
    /// superior e os de cada ocorrência de grupo repetível: o membro do grupo responde um item como
    /// qualquer outro, e uma restrição violada ali é tão inválida quanto no nível superior.
    /// </summary>
    private static IReadOnlyList<FieldError> RecusasEstruturais(AvaliacaoFormulario avaliacao) =>
    [
        .. avaliacao.Itens
            .Where(static item => item.RestricoesVioladas.Count > 0)
            .Select(static item => RecusaDoItem(item.FatoCodigo, item.FatoCodigo)),
        .. avaliacao.Grupos
            .SelectMany(static grupo => grupo.Ocorrencias.SelectMany(ocorrencia => ocorrencia.Itens
                .Where(static item => item.RestricoesVioladas.Count > 0)
                .Select(item => RecusaDoItem($"{grupo.Codigo}.{ocorrencia.Id}.{item.FatoCodigo}", item.FatoCodigo)))),
    ];

    private static FieldError RecusaDoItem(string campo, string fatoCodigo) => new(
        campo, new DomainError(InscricaoErrorCodes.RespostaInvalida, $"A resposta de '{fatoCodigo}' não atende a uma restrição do formulário."));
}
