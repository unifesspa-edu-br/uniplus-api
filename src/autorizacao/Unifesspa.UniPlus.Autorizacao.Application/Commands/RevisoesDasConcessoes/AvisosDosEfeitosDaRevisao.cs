namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Os avisos do que a revisão das concessões fez às concessões e à versão anterior: a migração e a revogação, avisadas ao
/// beneficiário uma vez por linha; as migrações que passam a aguardar o Representante do controlador, o envio de decisões com recusas e as
/// revogações das decisões, avisados aos grupos de quem decide; e a versão anterior encerrada. Nenhum leva lista de
/// beneficiários, motivo nem dado pessoal; o aviso nasce na transação que gravou o fato.
/// </summary>
public sealed class AvisosDosEfeitosDaRevisao
{
    private static readonly GrupoDaGovernanca[] GestaoERepresentantes = [GrupoDaGovernanca.Gestao, GrupoDaGovernanca.RepresentanteDoControlador];

    private readonly EmissorDeAvisos _emissor;
    private readonly GruposDaGovernanca _grupos;
    private readonly ILinkDaGovernanca _link;
    private readonly CatalogoDaAplicacao _catalogo;

    public AvisosDosEfeitosDaRevisao(EmissorDeAvisos emissor, GruposDaGovernanca grupos, ILinkDaGovernanca link, CatalogoDaAplicacao catalogo)
    {
        ArgumentNullException.ThrowIfNull(emissor);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(catalogo);

        _emissor = emissor;
        _grupos = grupos;
        _link = link;
        _catalogo = catalogo;
    }

    /// <summary>Os avisos de uma decisão humana: a quem migrou ou foi recusado e a quem decide o que vem depois.</summary>
    public async Task<IReadOnlyList<EnviarEmail>> DaDecisaoAsync(
        RevisaoDaOperacao revisao, MudancasDaRevisaoDasConcessoes mudancas, OperacaoNaRevisaoDasConcessoes operacao, Identidade ator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revisao);
        ArgumentNullException.ThrowIfNull(mudancas);
        ArgumentNullException.ThrowIfNull(ator);

        List<EnviarEmail> avisos = [];
        avisos.AddRange(await MigradasAsync(revisao, mudancas, cancellationToken).ConfigureAwait(false));
        avisos.AddRange(await AosBeneficiariosAsync(revisao, mudancas.Linhas.Recusadas, FormaDoAviso.Recusa, cancellationToken).ConfigureAwait(false));
        avisos.AddRange(await AguardandoAsync(revisao, mudancas, ator, cancellationToken).ConfigureAwait(false));
        avisos.AddRange(await RecusasAsync(revisao, mudancas, operacao, ator, cancellationToken).ConfigureAwait(false));

        return avisos;
    }

    /// <summary>
    /// Os avisos do fim da revisão, quando a operação a encerrou: a quem perdeu o acesso pelo fim e aos gestores de acessos
    /// e Representantes do controlador. Quem praticou o ato, se houve, não recebe o aviso do próprio ato.
    /// </summary>
    public async Task<IReadOnlyList<EnviarEmail>> DoFimAsync(
        RevisaoDaOperacao revisao, MudancasDaRevisaoDasConcessoes mudancas, Identidade? ator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revisao);
        ArgumentNullException.ThrowIfNull(mudancas);

        if (mudancas.Encerramento is not { } encerramento)
        {
            return [];
        }

        FormaDoAviso forma = FormaDoFim(encerramento);
        IReadOnlyList<EnviarEmail> revogadas = await AosBeneficiariosAsync(revisao, mudancas.Linhas.RevogadasPeloFim, forma, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> encerrada = await AosGruposAsync(
            DoEncerramento(revisao, mudancas, forma), revisao.Revisao.Id, GestaoERepresentantes, Excluir(ator), cancellationToken).ConfigureAwait(false);

        return [.. revogadas, .. encerrada];
    }

    /// <summary>A versão anterior que saiu de vigência sem revisão das concessões, avisada aos grupos, menos a quem praticou o ato.</summary>
    public Task<IReadOnlyList<EnviarEmail>> DaVersaoSubstituidaAsync(
        Perfil perfil, VersaoDoPerfil anterior, FormaDoAviso forma, Identidade autor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(anterior);
        ArgumentNullException.ThrowIfNull(autor);

        ModeloDeAviso modelo = new(TipoDeAviso.VersaoEncerrada, perfil.Nome, anterior.Numero, [], _link.DoPerfil(perfil.Id), Forma: forma);

        return AosGruposAsync(modelo, anterior.Id, GestaoERepresentantes, [autor], cancellationToken);
    }

    private static FormaDoAviso FormaDoFim(FormaDoEncerramento encerramento) => encerramento switch
    {
        FormaDoEncerramento.Concluida => FormaDoAviso.RevisaoConcluida,
        FormaDoEncerramento.Prazo => FormaDoAviso.FimDoPrazo,
        _ => FormaDoAviso.EncerramentoManual,
    };

    private ModeloDeAviso DoEncerramento(RevisaoDaOperacao revisao, MudancasDaRevisaoDasConcessoes mudancas, FormaDoAviso forma) =>
        new(
            TipoDeAviso.VersaoEncerrada, revisao.Perfil.Nome, NumeroDaVersaoAnterior(revisao), [], _link.DoPerfil(revisao.Perfil.Id),
            new ContagensDoAviso(Migradas: mudancas.TotalMigradas, Revogadas: mudancas.TotalRevogadas), Forma: forma);

    private static Identidade[] Excluir(Identidade? ator) => ator is null ? [] : [ator];

    // A versão anterior é a mais recente encerrada antes da versão nova.
    private static int NumeroDaVersaoAnterior(RevisaoDaOperacao revisao) =>
        revisao.Perfil.Versoes
            .Where(v => v.Situacao == SituacaoDaVersao.Encerrada && v.Numero < revisao.Revisao.NumeroDaVersaoNova)
            .Select(static v => (int?)v.Numero)
            .Max() ?? revisao.Revisao.NumeroDaVersaoNova - 1;

    private async Task<List<EnviarEmail>> MigradasAsync(RevisaoDaOperacao revisao, MudancasDaRevisaoDasConcessoes mudancas, CancellationToken cancellationToken)
    {
        int numeroDoAlvo = revisao.Contexto.Versao(revisao.Revisao.VersaoAlvoId).Numero;
        List<EnviarEmail> avisos = [];
        foreach (MudancaNaLinha linha in mudancas.Linhas.Migradas)
        {
            ModeloDeAviso modelo = new(
                TipoDeAviso.ConcessaoMigrada, revisao.Perfil.Nome, numeroDoAlvo, Nomes(linha.Acrescimo), _link.DosPedidos());
            avisos.AddRange(await AoBeneficiarioAsync(modelo, linha, cancellationToken).ConfigureAwait(false));
        }

        return avisos;
    }

    private async Task<List<EnviarEmail>> AosBeneficiariosAsync(
        RevisaoDaOperacao revisao, IReadOnlyList<MudancaNaLinha> linhas, FormaDoAviso forma, CancellationToken cancellationToken)
    {
        ModeloDeAviso modelo = new(TipoDeAviso.ConcessaoRevogada, revisao.Perfil.Nome, null, [], _link.DoNovoPedido(), Forma: forma);
        List<EnviarEmail> avisos = [];
        foreach (MudancaNaLinha linha in linhas)
        {
            avisos.AddRange(await AoBeneficiarioAsync(modelo, linha, cancellationToken).ConfigureAwait(false));
        }

        return avisos;
    }

    private Task<IReadOnlyList<EnviarEmail>> AoBeneficiarioAsync(ModeloDeAviso modelo, MudancaNaLinha linha, CancellationToken cancellationToken)
    {
        Concessao concessao = linha.Concessao;
        DestinatarioDoAviso[] beneficiario = concessao.BeneficiarioEmail is { } email ? [new DestinatarioDoAviso(concessao.Beneficiario, email)] : [];

        return _emissor.EmitirAsync(new PedidoDeAviso(modelo, linha.LinhaId, string.Empty, beneficiario, []), cancellationToken);
    }

    // As migrações que aguardam o Representante do controlador avisam também os gestores de acessos (UNI-REQ-0358).
    private Task<IReadOnlyList<EnviarEmail>> AguardandoAsync(
        RevisaoDaOperacao revisao, MudancasDaRevisaoDasConcessoes mudancas, Identidade ator, CancellationToken cancellationToken)
    {
        if (mudancas.Linhas.Aguardando.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<EnviarEmail>>([]);
        }

        ModeloDeAviso modelo = ModeloDaRevisao(TipoDeAviso.MigracoesAguardandoProtecaoDeDados, revisao, new ContagensDoAviso(Aguardando: mudancas.Linhas.Aguardando.Count));

        return AosGruposAsync(modelo, mudancas.NovoEnvioId ?? mudancas.Linhas.Aguardando[0].LinhaId, GestaoERepresentantes, [ator], cancellationToken);
    }

    // O envio de decisões avisa os gestores de acessos e os Representantes do controlador; a retida e a etapa protegida, só os gestores.
    private Task<IReadOnlyList<EnviarEmail>> RecusasAsync(
        RevisaoDaOperacao revisao, MudancasDaRevisaoDasConcessoes mudancas, OperacaoNaRevisaoDasConcessoes operacao, Identidade ator, CancellationToken cancellationToken)
    {
        if (mudancas.Linhas.Recusadas.Count == 0 || operacao == OperacaoNaRevisaoDasConcessoes.Encerramento)
        {
            return Task.FromResult<IReadOnlyList<EnviarEmail>>([]);
        }

        bool envioDaGestao = operacao == OperacaoNaRevisaoDasConcessoes.EnvioDaGestao;
        ModeloDeAviso modelo = ModeloDaRevisao(
            envioDaGestao ? TipoDeAviso.DecisoesEnviadasComRecusas : TipoDeAviso.RevogacoesNaMigracao, revisao, new ContagensDoAviso(Revogadas: mudancas.Linhas.Recusadas.Count));

        return AosGruposAsync(
            modelo, mudancas.NovoEnvioId ?? mudancas.Linhas.Recusadas[0].LinhaId, envioDaGestao ? GestaoERepresentantes : [GrupoDaGovernanca.Gestao], [ator], cancellationToken);
    }

    private async Task<IReadOnlyList<EnviarEmail>> AosGruposAsync(
        ModeloDeAviso modelo, Guid fatoId, IEnumerable<GrupoDaGovernanca> grupos, IReadOnlyCollection<Identidade> excluidos, CancellationToken cancellationToken)
    {
        List<DestinatarioDoAviso> destinatarios = await _grupos.ListarAsync(grupos, cancellationToken).ConfigureAwait(false);

        return await _emissor
            .EmitirAsync(new PedidoDeAviso(modelo, fatoId, string.Empty, destinatarios, excluidos), cancellationToken)
            .ConfigureAwait(false);
    }

    private ModeloDeAviso ModeloDaRevisao(TipoDeAviso tipo, RevisaoDaOperacao revisao, ContagensDoAviso contagens) =>
        new(tipo, revisao.Perfil.Nome, revisao.Revisao.NumeroDaVersaoNova, [], _link.DaRevisao(revisao.Perfil.Id), contagens, revisao.Revisao.TerminaEm);

    private List<string> Nomes(IEnumerable<string> codigos) => [.. codigos.Select(c => PerfilMapping.Descrever(c, _catalogo).Nome)];
}
