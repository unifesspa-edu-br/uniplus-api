namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Events;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma inscrição ativa por candidato e processo (UNI-REQ-0023). Nasce rascunho, é enviada com número e comprovante
/// imutável e pode ser cancelada. Corrigir emite outro comprovante sem alterar os anteriores.
/// </summary>
/// <remarks>
/// O rascunho adota a versão divulgada; o primeiro envio a fixa (UNI-REQ-0084). Cada valor gravado tem definição,
/// que determina a cifra dos identificadores na persistência (ADR-0121). A revisão e a versão compõem o ETag;
/// a revisão também detecta gravações concorrentes no banco após a conferência do If-Match (ADR-0119).
/// </remarks>
public sealed class Inscricao : EntityBase
{
    public Guid ProcessoSeletivoId { get; private set; }

    public TitularDaInscricao Titular { get; private set; } = null!;

    public EstadoDaInscricao Estado { get; private set; } = null!;

    public FormularioDaInscricao Formulario { get; private set; } = null!;

    /// <summary>
    /// Nasce em <b>1</b> e é incrementada por toda mutação aceita (<see cref="Salvar"/> e
    /// <see cref="Submeter"/>) — token de concorrência, ver os comentários da classe.
    /// </summary>
    public int Revisao { get; private set; }

    private Inscricao() { }

    /// <summary>
    /// Cria o rascunho na versão do formulário divulgada no instante do início, já com a resposta do CPF da conta: a
    /// inscrição não tem outra guarda do CPF, e é por essa resposta que ela volta a ser lida.
    /// </summary>
    public static Result<Inscricao> Iniciar(
        Guid processoSeletivoId,
        Cpf cpf,
        string contaId,
        string emissorToken,
        Guid versaoFormularioId)
    {
        Result<TitularDaInscricao> titular = TitularDaInscricao.Identificar(cpf, contaId, emissorToken);
        if (titular.IsFailure)
        {
            return Result<Inscricao>.Failure(titular.Error!);
        }

        return Result<Inscricao>.Success(CriarInicial(processoSeletivoId, cpf, titular.Value!, versaoFormularioId));
    }

    private static Inscricao CriarInicial(Guid processoSeletivoId, Cpf cpf, TitularDaInscricao titular, Guid versaoFormularioId) => new()
    {
        ProcessoSeletivoId = processoSeletivoId,
        Titular = titular,
        Estado = EstadoDaInscricao.Inicial,
        Formulario = FormularioDaInscricao.Inicial(versaoFormularioId, cpf),
        Revisao = 1,
    };

    /// <summary>Reconstrói o estado persistido; os carimbos de gravação pertencem ao registro do repositório.</summary>
    public static Inscricao Reidratar(Guid id, Guid processoSeletivoId, TitularDaInscricao titular,
        EstadoDaInscricao estado, FormularioDaInscricao formulario, int revisao, DatasDeGravacaoDaInscricao datas)
    {
        ArgumentNullException.ThrowIfNull(titular);
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(formulario);
        return new Inscricao
        {
            Id = id,
            ProcessoSeletivoId = processoSeletivoId,
            Titular = titular,
            Estado = estado,
            Formulario = formulario,
            Revisao = revisao,
            CreatedAt = datas.CriadaEm,
            UpdatedAt = datas.AtualizadaEm,
        };
    }

    /// <summary>
    /// O ETag forte da revisão corrente — identidade da inscrição dentro dele (sem ela, descartar e recriar reinicia a revisão e um ETag antigo valeria na nova: ABA)
    /// e a versão do formulário, porque o rascunho adotado na memória e o gravado podem estar na mesma revisão em versões diferentes.
    /// </summary>
    public string ETag => $"\"{Id}:{Revisao}:{Formulario.VersaoFormularioId}\"";

    /// <summary>Confere o <c>If-Match</c> contra o <see cref="ETag"/> corrente — <see langword="null"/> quando a mutação pode prosseguir.</summary>
    public DomainError? ConferirPrecondicao(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        return !precondicao.Presente
            ? new DomainError(InscricaoErrorCodes.PrecondicaoRequerida,
                "Informe o If-Match com o ETag da inscrição antes de salvar ou enviar.")
            : !precondicao.Casa(ETag)
                ? new DomainError(InscricaoErrorCodes.PrecondicaoFalhou,
                    "O If-Match informado não corresponde ao estado atual da inscrição — releia e refaça a operação.")
                : null;
    }

    /// <summary>A recusa de quem não tem inscrição no processo. Não cita o CPF nem o processo: vai para a resposta e para o log.</summary>
    public static DomainError NaoEncontrada { get; } = new(
        InscricaoErrorCodes.NaoEncontrada, "Você ainda não tem inscrição neste processo seletivo.");

    /// <summary>Ainda é rascunho: a inscrição enviada só muda por novo envio, e a cancelada não muda mais.</summary>
    public bool EhRascunho => Estado.Situacao == SituacaoInscricao.Rascunho;

    /// <summary>Depois do envio, a inscrição só muda por novo envio, que emite novo comprovante; depois do cancelamento, não muda.</summary>
    public DomainError? ConferirAindaEmRascunho() =>
        ConferirNaoCancelada()
        ?? (Estado.Situacao == SituacaoInscricao.Enviada
            ? new DomainError(
                InscricaoErrorCodes.RascunhoSoAntesDoEnvio,
                "Sua inscrição já foi enviada. Para corrigi-la, envie de novo: cada envio gera um novo comprovante.")
            : null);

    /// <summary>A inscrição cancelada não salva, não envia, não corrige nem adota versão: para se inscrever de novo, inicia-se outra.</summary>
    public DomainError? ConferirNaoCancelada() =>
        Estado.Situacao == SituacaoInscricao.Cancelada
            ? new DomainError(
                InscricaoErrorCodes.InscricaoCancelada,
                "Esta inscrição foi cancelada e não pode mais ser alterada. Para participar, inicie uma nova inscrição.")
            : null;

    /// <summary>A inscrição cancelada não tem comprovante para exibir nem imprimir; os já emitidos ficam preservados, sem exibição.</summary>
    public DomainError? ConferirComprovanteEmitivel() => Estado.Situacao == SituacaoInscricao.Cancelada
        ? new DomainError(InscricaoErrorCodes.ComprovanteDeInscricaoCancelada, "Esta inscrição foi cancelada e não tem comprovante para exibir nem imprimir.")
        : null;

    /// <summary>
    /// Confere o descarte do rascunho: inscrição ainda não enviada e <c>If-Match</c> corrente — a recusa definitiva vem antes da que pede releitura. A enviada
    /// tem número e comprovante, e sai pelo cancelamento — nunca por remoção.
    /// </summary>
    public DomainError? ConferirDescarte(PrecondicaoIfMatch precondicao)
    {
        if (ConferirNaoCancelada() is { } cancelada)
        {
            return cancelada;
        }

        if (Estado.Situacao == SituacaoInscricao.Enviada)
        {
            return new DomainError(
                InscricaoErrorCodes.DescarteSoDeRascunho,
                "Sua inscrição já foi enviada e não pode ser descartada. Para desistir dela, cancele a inscrição.");
        }

        return ConferirPrecondicao(precondicao);
    }

    /// <summary>
    /// Só a inscrição enviada se cancela: o rascunho sai pelo descarte, e a cancelada já saiu. A recusa
    /// definitiva vem antes da que pede releitura; o período é conferido antes por quem chama, porque fora dele a orientação levaria a outra recusa.
    /// </summary>
    public DomainError? ConferirCancelamento() =>
        ConferirNaoCancelada()
        ?? (Estado.Situacao == SituacaoInscricao.Rascunho
            ? new DomainError(
                InscricaoErrorCodes.CancelamentoSoDeEnviada,
                "Só a inscrição enviada se cancela. Para desistir do rascunho, descarte-o.")
            : null);

    /// <summary>
    /// Cancela a inscrição enviada: grava quando, quem e por quê (sem motivo, <see cref="CancelamentoDaInscricao.MotivoPadraoDoCancelamento"/>)
    /// e mantém o número, que não é reaproveitado por outra inscrição. O período é conferido por quem chama.
    /// </summary>
    public Result Cancelar(string? motivo, string ator, DateTimeOffset instante, PrecondicaoIfMatch precondicao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ator);
        ArgumentNullException.ThrowIfNull(precondicao);

        if (ConferirCancelamento() is { } recusa) return Result.Failure(recusa);
        if (ConferirPrecondicao(precondicao) is { } erroPrecondicao) return Result.Failure(erroPrecondicao);
        Result<CancelamentoDaInscricao> cancelamento = CancelamentoDaInscricao.Registrar(motivo, ator, instante);
        if (cancelamento.IsFailure) return Result.Failure(cancelamento.Error!);
        AplicarCancelamento(cancelamento.Value!, instante);
        return Result.Success();
    }

    private void AplicarCancelamento(CancelamentoDaInscricao cancelamento, DateTimeOffset instante)
    {
        Estado = Estado.CancelarAposConferencia(cancelamento);
        Revisao++;
        AddDomainEvent(new InscricaoCanceladaEvent(Id, ProcessoSeletivoId, Estado.NumeroInscricao!, instante));
    }

    /// <summary>
    /// O que o candidato pode fazer com a inscrição, dado se as inscrições estão abertas. Corrigir e
    /// cancelar pedem inscrição enviada e período aberto; continuar e descartar pedem rascunho e período aberto.
    /// </summary>
    public AcoesDaInscricao AcoesPermitidas(bool inscricoesAbertas) => new(
        PodeContinuar: Estado.Situacao == SituacaoInscricao.Rascunho && inscricoesAbertas,
        PodeCorrigir: Estado.Situacao == SituacaoInscricao.Enviada && inscricoesAbertas,
        PodeCancelar: Estado.Situacao == SituacaoInscricao.Enviada && inscricoesAbertas,
        PodeDescartar: Estado.Situacao == SituacaoInscricao.Rascunho && inscricoesAbertas);

    /// <summary>
    /// O rascunho passa para a versão divulgada <paramref name="versaoVigenteId"/>: troca a versão e tira o
    /// que ela não admite, sem recusa, porque o candidato não escolheu a retificação. Saem as respostas e
    /// os grupos de fatos que a versão não pede, os aceites de termo de outra versão ou hash, a oferta
    /// que ela não tem e a 2ª opção que deixou de valer (a versão passou a admitir uma só, ou a 1ª saiu).
    /// A revisão sobe, e com ela o ETag. Na mesma versão, nada muda. A inscrição enviada não troca de
    /// versão: a recusa é do agregado.
    /// </summary>
    public Result AdotarVersao(Guid versaoVigenteId, RespostasAdmitidas admitidas)
    {
        ArgumentNullException.ThrowIfNull(admitidas);

        if (ConferirAindaEmRascunho() is { } erroRascunho)
        {
            return Result.Failure(erroRascunho);
        }

        if (versaoVigenteId == Formulario.VersaoFormularioId)
        {
            return Result.Success();
        }

        AplicarVersao(versaoVigenteId, admitidas);
        return Result.Success();
    }

    private void AplicarVersao(Guid versaoVigenteId, RespostasAdmitidas admitidas)
    {
        Formulario = Formulario.NaVersao(versaoVigenteId);
        Formulario = Formulario.ComPrimeiraAdmitida(admitidas);
        Formulario = Formulario.ComSegundaAdmitida(admitidas);
        Formulario = Formulario.ComRespostasAdmitidas(admitidas);
        Formulario = Formulario.ComGruposAdmitidos(admitidas);
        Formulario = Formulario.ComAceitesAdmitidos(admitidas);
        Formulario = Formulario.ComDefinicoes(admitidas.Definicoes);
        Revisao++;
    }

    /// <summary>
    /// Salva o rascunho: validação estrutural, sem exigir obrigatórios — a validação completa é da
    /// submissão (issue #1859). Preserva um número de documento ausente do corpo: ausência nunca
    /// apaga, só um valor enviado substitui. Depois do envio, a inscrição que vale é a enviada
    /// (UNI-REQ-0132): só um novo envio, com novo comprovante, a muda.
    /// </summary>
    public Result Salvar(PreenchimentoDoCandidato recebido, RespostasAdmitidas admitidas, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(recebido);
        ArgumentNullException.ThrowIfNull(recebido.Respostas, nameof(recebido));
        ArgumentNullException.ThrowIfNull(recebido.Grupos, nameof(recebido));
        ArgumentNullException.ThrowIfNull(recebido.Aceites, nameof(recebido));
        ArgumentNullException.ThrowIfNull(admitidas);
        if (ConferirPrecondicao(precondicao) is { } erroPrecondicao) return Result.Failure(erroPrecondicao);
        if (ConferirAindaEmRascunho() is { } erroRascunho) return Result.Failure(erroRascunho);
        List<FieldError> recusas = RecusasAoSalvar(recebido, admitidas);
        if (recusas.Count > 0) return Result.ValidationFailure(recusas);
        recebido = PreenchimentoDoCandidato.Criar(recebido.Opcoes, recebido.Respostas, recebido.Grupos, recebido.Aceites);
        AplicarSalvamento(recebido, admitidas);
        return Result.Success();
    }

    private List<FieldError> RecusasAoSalvar(PreenchimentoDoCandidato recebido, RespostasAdmitidas admitidas) =>
    [
        .. recebido.RecusasDeForma(admitidas),
        .. Titular.RecusasDoCpf(recebido.Respostas),
        .. ConferenciaDosNomesDoTitular.RecusasDoNome(recebido.Respostas, admitidas),
        .. ConferenciaDosNomesDoTitular.RecusasDoNomeSocialAoSalvar(recebido.Respostas, admitidas),
        .. recebido.RecusasDoEndereco(admitidas),
        .. recebido.Opcoes.RecusasAoSalvar(admitidas),
        .. recebido.RecusasDosAceites(admitidas.Termos),
    ];

    private void AplicarSalvamento(PreenchimentoDoCandidato recebido, RespostasAdmitidas admitidas)
    {
        Formulario = Formulario.ComOpcoes(recebido.Opcoes);
        Formulario = Formulario.SalvoComRespostas(recebido.Respostas, admitidas);
        Formulario = Formulario.SalvoComGrupos(recebido.Grupos, admitidas);
        Formulario = Formulario.ComAceites(recebido.Aceites);
        Formulario = Formulario.ComDefinicoes(admitidas.Definicoes);
        Revisao++;
    }

    /// <summary>
    /// Envia ou corrige a inscrição, atribuindo o número no primeiro envio e emitindo um comprovante imutável.
    /// Só a raiz muda o estado e a revisão e emite o evento correspondente ao envio.
    /// </summary>
    /// <remarks>
    /// A avaliação em <paramref name="apuracao"/> decide os dados aplicáveis (UNI-REQ-0076): respostas e grupos ocultos
    /// são descartados no envio, pois guardá-los seria reter dado pessoal sem finalidade. O rascunho os conserva para
    /// permitir rever as respostas. O comprovante registra as modalidades com os nomes congelados na publicação.
    /// </remarks>
    public Result<ComprovanteInscricao> Submeter(
        string numeroGeradoSeNecessario, PreenchimentoDoCandidato recebido, RespostasAdmitidas admitidas,
        ApuracaoDoEnvio apuracao, PrecondicaoIfMatch precondicao, DateTimeOffset instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroGeradoSeNecessario);
        ArgumentNullException.ThrowIfNull(recebido);
        ArgumentNullException.ThrowIfNull(admitidas);
        ArgumentNullException.ThrowIfNull(apuracao);
        if (ConferirNaoCancelada() is { } cancelada) return Result<ComprovanteInscricao>.Failure(cancelada);
        if (ConferirPrecondicao(precondicao) is { } recusada) return Result<ComprovanteInscricao>.Failure(recusada);
        IReadOnlyList<FieldError> recusas = [.. RecusasDoEnvio(recebido, admitidas, apuracao),
            .. ModalidadesDoEnvio.DaAvaliacao(apuracao.Avaliacao, recebido.Opcoes).Recusas(recebido.Opcoes, admitidas)];
        return recusas.Count > 0 ? Result<ComprovanteInscricao>.ValidationFailure(recusas)
            : AplicarEnvio(numeroGeradoSeNecessario, recebido, admitidas, apuracao, instante);
    }

    private Result<ComprovanteInscricao> AplicarEnvio(string numeroGerado, PreenchimentoDoCandidato recebido,
        RespostasAdmitidas admitidas, ApuracaoDoEnvio apuracao, DateTimeOffset instante)
    {
        bool primeiraSubmissao = Estado.NumeroInscricao is null;
        string numero = Estado.NumeroInscricao ?? numeroGerado;
        Estado = Estado.AtribuirNumeroSeNecessario(numero);
        Formulario = Formulario.ComOpcoes(recebido.Opcoes);
        ModalidadesDoEnvio modalidades = ModalidadesDoEnvio.DaAvaliacao(apuracao.Avaliacao, recebido.Opcoes);
        Dictionary<string, JsonElement> derivados = modalidades.ComDerivados(apuracao.Derivados);
        Dictionary<string, DefinicaoDaResposta> definicoes = modalidades.ComDefinicoes(apuracao.DefinicoesDosDerivados);
        Formulario = Formulario.EnviadoComDerivados(derivados, definicoes, apuracao.Municipios ?? MunicipiosConferidos.Nenhum);
        AplicarRespostasDoEnvio(recebido, admitidas, apuracao, definicoes, numero);
        ComprovanteInscricao comprovante = EmitirComprovante(numero, instante, apuracao, modalidades);
        AddDomainEvent(primeiraSubmissao
            ? (IDomainEvent)new InscricaoSubmetidaEvent(Id, ProcessoSeletivoId, numero, instante)
            : new InscricaoCorrigidaEvent(Id, ProcessoSeletivoId, numero, instante));
        return Result<ComprovanteInscricao>.Success(comprovante);
    }

    private void AplicarRespostasDoEnvio(PreenchimentoDoCandidato recebido, RespostasAdmitidas admitidas,
        ApuracaoDoEnvio apuracao, Dictionary<string, DefinicaoDaResposta> definicoes, string numero)
    {
        MunicipiosConferidos conferidos = apuracao.Municipios ?? MunicipiosConferidos.Nenhum;
        Formulario = Formulario.EnviadoComRespostas(recebido.Respostas, admitidas, apuracao.Avaliacao, conferidos);
        Formulario = Formulario.EnviadoComGrupos(recebido.Grupos, admitidas, apuracao.Avaliacao, conferidos);
        Formulario = Formulario.ComAceites(recebido.Aceites);
        Formulario = Formulario.ComDefinicoes(admitidas.Definicoes.Concat(definicoes)
            .GroupBy(static f => f.Key, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Last().Value, StringComparer.Ordinal));
        Estado = Estado.SubmeterAposConferencia(numero);
        Revisao++;
    }

    private ComprovanteInscricao EmitirComprovante(string numero, DateTimeOffset instante, ApuracaoDoEnvio apuracao, ModalidadesDoEnvio modalidades) =>
        ComprovanteInscricao.Emitir(Id, numero, instante, Formulario.Preenchimento.Opcoes.Primeira,
            Formulario.SemCifraIndividual(Formulario.SemNumerosDeDocumento(Formulario.Preenchimento.Respostas)),
            Formulario.SemNumerosDeDocumento(Formulario.Derivados)
                .Where(static d => d.Key is not (AvaliacaoDaInscricao.FatoDaModalidade or AvaliacaoDaInscricao.FatoDaModalidadeDaSegundaOpcao))
                .ToDictionary(static d => d.Key, static d => d.Value, StringComparer.Ordinal),
            Formulario.SemNumerosDeDocumentoNosGrupos(Formulario.Preenchimento.Grupos), Formulario.Preenchimento.Aceites,
            apuracao.DocumentosDoComprovante(), modalidades.Primeira.Derivada ? modalidades.Primeira.Itens : null,
            Formulario.ApenasCifraIndividual(Formulario.SemNumerosDeDocumento(Formulario.Preenchimento.Respostas)),
            Formulario.Preenchimento.Opcoes.Segunda, modalidades.Segunda is { Derivada: true } ? modalidades.Segunda.Itens : null);

    /// <summary>Acumula as recusas locais na ordem do envio, antes das modalidades e das conferências externas.</summary>
    public IReadOnlyList<FieldError> RecusasDoEnvio(PreenchimentoDoCandidato recebido, RespostasAdmitidas admitidas,
        ApuracaoDoEnvio apuracao, IReadOnlySet<(Guid ExigenciaId, string? OcorrenciaId)>? documentosEmVerificacao = null)
    {
        ArgumentNullException.ThrowIfNull(recebido);
        ArgumentNullException.ThrowIfNull(admitidas);
        ArgumentNullException.ThrowIfNull(apuracao);
        IReadOnlyDictionary<string, JsonElement> mescladas = Formulario.Preenchimento.RespostasAposMesclar(recebido.Respostas);
        return
        [
            .. recebido.RecusasDeForma(admitidas),
            .. apuracao.RecusasDosDerivados(),
            .. Titular.RecusasDoCpf(recebido.Respostas),
            .. RecusasDoPerfilEnviado(recebido, mescladas, admitidas),
            .. recebido.Opcoes.RecusasAoEnviar(admitidas),
            .. apuracao.Avaliacao.Documentos.RecusasDosAusentes(documentosEmVerificacao),
            .. recebido.RecusasDosAceites(admitidas.Termos),
        ];
    }

    private static IEnumerable<FieldError> RecusasDoPerfilEnviado(PreenchimentoDoCandidato recebido,
        IReadOnlyDictionary<string, JsonElement> mescladas, RespostasAdmitidas admitidas) =>
    [
        .. ConferenciaDosNomesDoTitular.RecusasDoNome(mescladas, admitidas),
        .. ConferenciaDosNomesDoTitular.RecusasDoNomeSocialAoEnviar(mescladas, admitidas),
        .. PreenchimentoDoCandidato.ParaValidacao(recebido.Opcoes, mescladas, recebido.Grupos, recebido.Aceites).RecusasDoEndereco(admitidas),
    ];

}
