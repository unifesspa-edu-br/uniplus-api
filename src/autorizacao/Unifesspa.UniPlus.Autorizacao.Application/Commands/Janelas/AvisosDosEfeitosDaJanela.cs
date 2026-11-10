namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Os avisos do que a janela de migração fez às concessões e à versão anterior: a migração e a revogação, avisadas ao
/// beneficiário uma vez por linha; as migrações que passam a aguardar a proteção de dados, o lote com recusas e as
/// revogações das decisões, avisados aos grupos de quem decide; e a versão anterior encerrada. Nenhum leva lista de
/// beneficiários, motivo nem dado pessoal; o aviso nasce na transação que gravou o fato.
/// </summary>
public sealed class AvisosDosEfeitosDaJanela
{
    private static readonly GrupoDaGovernanca[] GestaoEProtecao = [GrupoDaGovernanca.Gestao, GrupoDaGovernanca.ProtecaoDeDados];

    private readonly EmissorDeAvisos _emissor;
    private readonly GruposDaGovernanca _grupos;
    private readonly ILinkDaGovernanca _link;
    private readonly CatalogoDaAplicacao _catalogo;

    public AvisosDosEfeitosDaJanela(EmissorDeAvisos emissor, GruposDaGovernanca grupos, ILinkDaGovernanca link, CatalogoDaAplicacao catalogo)
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
        JanelaDaOperacao janela, MudancasDaJanela mudancas, OperacaoNaJanela operacao, Identidade ator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(mudancas);
        ArgumentNullException.ThrowIfNull(ator);

        List<EnviarEmail> avisos = [];
        avisos.AddRange(await MigradasAsync(janela, mudancas, cancellationToken).ConfigureAwait(false));
        avisos.AddRange(await AosBeneficiariosAsync(janela, mudancas.Recusadas, FormaDoAviso.Recusa, cancellationToken).ConfigureAwait(false));
        avisos.AddRange(await AguardandoAsync(janela, mudancas, ator, cancellationToken).ConfigureAwait(false));
        avisos.AddRange(await RecusasAsync(janela, mudancas, operacao, ator, cancellationToken).ConfigureAwait(false));

        return avisos;
    }

    /// <summary>
    /// Os avisos do fim da janela, quando a operação a encerrou: a quem perdeu o acesso pelo fim e aos grupos de gestão e
    /// proteção de dados. Quem praticou o ato, se houve, não recebe o aviso do próprio ato.
    /// </summary>
    public async Task<IReadOnlyList<EnviarEmail>> DoFimAsync(
        JanelaDaOperacao janela, MudancasDaJanela mudancas, Identidade? ator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(mudancas);

        if (mudancas.Encerramento is not { } encerramento)
        {
            return [];
        }

        FormaDoAviso forma = FormaDoFim(encerramento);
        IReadOnlyList<EnviarEmail> revogadas = await AosBeneficiariosAsync(janela, mudancas.RevogadasPeloFim, forma, cancellationToken).ConfigureAwait(false);
        ContagensDoAviso contagens = new(Migradas: mudancas.TotalMigradas, Revogadas: mudancas.TotalRevogadas);
        ModeloDeAviso modelo = new(
            TipoDeAviso.VersaoEncerrada, janela.Perfil.Nome, NumeroDaVersaoAnterior(janela), [], _link.DoPerfil(janela.Perfil.Id), contagens, Forma: forma);
        IReadOnlyList<EnviarEmail> encerrada = await AosGruposAsync(modelo, janela.Janela.Id, GestaoEProtecao, Excluir(ator), cancellationToken).ConfigureAwait(false);

        return [.. revogadas, .. encerrada];
    }

    /// <summary>A versão anterior que saiu de vigência sem janela de migração, avisada aos grupos, menos a quem praticou o ato.</summary>
    public Task<IReadOnlyList<EnviarEmail>> DaVersaoSubstituidaAsync(
        Perfil perfil, VersaoDoPerfil anterior, FormaDoAviso forma, Identidade autor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(anterior);
        ArgumentNullException.ThrowIfNull(autor);

        ModeloDeAviso modelo = new(TipoDeAviso.VersaoEncerrada, perfil.Nome, anterior.Numero, [], _link.DoPerfil(perfil.Id), Forma: forma);

        return AosGruposAsync(modelo, anterior.Id, GestaoEProtecao, [autor], cancellationToken);
    }

    private static FormaDoAviso FormaDoFim(FormaDoEncerramento encerramento) => encerramento switch
    {
        FormaDoEncerramento.Concluida => FormaDoAviso.JanelaConcluida,
        FormaDoEncerramento.Prazo => FormaDoAviso.FimDoPrazo,
        _ => FormaDoAviso.EncerramentoManual,
    };

    private static Identidade[] Excluir(Identidade? ator) => ator is null ? [] : [ator];

    // A versão anterior é a mais recente encerrada antes da versão nova.
    private static int NumeroDaVersaoAnterior(JanelaDaOperacao janela) =>
        janela.Perfil.Versoes
            .Where(v => v.Situacao == SituacaoDaVersao.Encerrada && v.Numero < janela.Janela.NumeroDaVersaoNova)
            .Select(static v => (int?)v.Numero)
            .Max() ?? janela.Janela.NumeroDaVersaoNova - 1;

    private async Task<List<EnviarEmail>> MigradasAsync(JanelaDaOperacao janela, MudancasDaJanela mudancas, CancellationToken cancellationToken)
    {
        int numeroDoAlvo = janela.Contexto.Versao(janela.Janela.VersaoAlvoId).Numero;
        List<EnviarEmail> avisos = [];
        foreach (MudancaNaLinha linha in mudancas.Migradas)
        {
            ModeloDeAviso modelo = new(
                TipoDeAviso.ConcessaoMigrada, janela.Perfil.Nome, numeroDoAlvo, Nomes(linha.Acrescimo), _link.DasConcessoes());
            avisos.AddRange(await AoBeneficiarioAsync(modelo, linha, cancellationToken).ConfigureAwait(false));
        }

        return avisos;
    }

    private async Task<List<EnviarEmail>> AosBeneficiariosAsync(
        JanelaDaOperacao janela, IReadOnlyList<MudancaNaLinha> linhas, FormaDoAviso forma, CancellationToken cancellationToken)
    {
        ModeloDeAviso modelo = new(TipoDeAviso.ConcessaoRevogada, janela.Perfil.Nome, null, [], _link.DoNovoPedido(), Forma: forma);
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

    private Task<IReadOnlyList<EnviarEmail>> AguardandoAsync(
        JanelaDaOperacao janela, MudancasDaJanela mudancas, Identidade ator, CancellationToken cancellationToken)
    {
        if (mudancas.Aguardando.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<EnviarEmail>>([]);
        }

        ModeloDeAviso modelo = ModeloDaJanela(TipoDeAviso.MigracoesAguardandoProtecaoDeDados, janela, new ContagensDoAviso(Aguardando: mudancas.Aguardando.Count));

        return AosGruposAsync(modelo, mudancas.NovoLoteId ?? mudancas.Aguardando[0].LinhaId, [GrupoDaGovernanca.ProtecaoDeDados], [ator], cancellationToken);
    }

    // O lote avisa gestão e proteção de dados; a retida e a etapa da proteção, só a gestão.
    private Task<IReadOnlyList<EnviarEmail>> RecusasAsync(
        JanelaDaOperacao janela, MudancasDaJanela mudancas, OperacaoNaJanela operacao, Identidade ator, CancellationToken cancellationToken)
    {
        if (mudancas.Recusadas.Count == 0 || operacao == OperacaoNaJanela.Encerramento)
        {
            return Task.FromResult<IReadOnlyList<EnviarEmail>>([]);
        }

        bool lote = operacao == OperacaoNaJanela.Lote;
        ModeloDeAviso modelo = ModeloDaJanela(
            lote ? TipoDeAviso.LoteEnviadoComRecusas : TipoDeAviso.RevogacoesNaMigracao, janela, new ContagensDoAviso(Revogadas: mudancas.Recusadas.Count));

        return AosGruposAsync(
            modelo, mudancas.NovoLoteId ?? mudancas.Recusadas[0].LinhaId, lote ? GestaoEProtecao : [GrupoDaGovernanca.Gestao], [ator], cancellationToken);
    }

    private async Task<IReadOnlyList<EnviarEmail>> AosGruposAsync(
        ModeloDeAviso modelo, Guid fatoId, IEnumerable<GrupoDaGovernanca> grupos, IReadOnlyCollection<Identidade> excluidos, CancellationToken cancellationToken)
    {
        List<DestinatarioDoAviso> destinatarios = await _grupos.ListarAsync(grupos, cancellationToken).ConfigureAwait(false);

        return await _emissor
            .EmitirAsync(new PedidoDeAviso(modelo, fatoId, string.Empty, destinatarios, excluidos), cancellationToken)
            .ConfigureAwait(false);
    }

    private ModeloDeAviso ModeloDaJanela(TipoDeAviso tipo, JanelaDaOperacao janela, ContagensDoAviso contagens) =>
        new(tipo, janela.Perfil.Nome, janela.Janela.NumeroDaVersaoNova, [], _link.DaJanela(janela.Perfil.Id), contagens, janela.Janela.Prazo);

    private List<string> Nomes(IEnumerable<string> codigos) => [.. codigos.Select(c => PerfilMapping.Descrever(c, _catalogo).Nome)];
}
