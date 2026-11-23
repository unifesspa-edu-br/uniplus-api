namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O assunto dos motivos de decisão escolhidos na conferência da configuração, na ordem do motivo desativado no cadastro, do
/// motivo de tipo que o lugar não declara e da cobertura (UNI-REQ-0122, UNI-REQ-0123); o motivo recusado pelas duas primeiras
/// não conta para a cobertura, e o painel tem um item por regra.
/// </summary>
public sealed class AssuntoDosMotivosDeDecisaoTests
{
    private const int VersaoQueNasce = 1;

    [Fact(DisplayName = "O assunto confere na ordem o motivo desativado, o de tipo não declarado e a cobertura, e o recusado não conta para a cobertura")]
    public void Assunto_ConfereNaOrdemERecusadoNaoContaParaACobertura()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDeDecisao desativada = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "FAVORAVEL_DESATIVADO");
        MotivoDeDecisaoDoProcesso desativado = Escolha(processo, "INSCRICAO", desativada);
        desativada.Desativar();
        MotivoDeDecisao semDeclarar = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "DESFAVORAVEL_NA_ETAPA");
        MotivoDeDecisaoDoProcesso naEtapa = Escolha(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), semDeclarar);

        FieldError[] causas = Assunto(processo, new MotivoEscolhidoComOrigem(desativado, desativada), new MotivoEscolhidoComOrigem(naEtapa, semDeclarar));

        causas.Select(c => c.Error.Code).Should().Equal(
            MotivoDeDecisaoErrorCodes.MotivoDesativado,
            MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado,
            MotivoDeDecisaoErrorCodes.CoberturaInsuficiente,
            MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
    }

    [Fact(DisplayName = "O assunto não tem causa quando os motivos escolhidos são ativos, de tipo declarado e cobrem o fundamento")]
    public void Assunto_SemCausa_QuandoOsMotivosCobrem()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDeDecisao favoravel = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "NIS_LOCALIZADO");
        MotivoDeDecisao desfavoravel = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "NIS_NAO_LOCALIZADO");

        FieldError[] causas = Assunto(
            processo,
            new MotivoEscolhidoComOrigem(Escolha(processo, "INSCRICAO", favoravel), favoravel),
            new MotivoEscolhidoComOrigem(Escolha(processo, "INSCRICAO", desfavoravel), desfavoravel));

        causas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo já publicado desativado depois continua contando para a cobertura da versão que nasce")]
    public void Assunto_PublicadoDesativadoDepois_ContaParaACobertura()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDeDecisao favoravel = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "NIS_LOCALIZADO");
        MotivoDeDecisao desfavoravel = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "NIS_NAO_LOCALIZADO");
        MotivoEscolhidoComOrigem[] publicados =
        [
            new(ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), favoravel, versaoDeEntrada: 1), favoravel),
            new(ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), desfavoravel, versaoDeEntrada: 1), desfavoravel),
        ];
        favoravel.Desativar();

        Assunto(processo, 2, publicados).Should().BeEmpty();
    }

    [Fact(DisplayName = "Um processo que não escolheu motivo e não cobra taxa não tem causa dos motivos")]
    public void Assunto_SemMotivosESemTaxa_SemCausa()
    {
        Assunto(ProcessoParaMotivos.Rascunho()).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo desativado de fase ou etapa que a configuração lida não traz é causa do assunto e pende o item do painel")]
    public void Assunto_MotivoDesativadoEmLugarForaDaConfiguracao_TemCausaEPendeOPainel()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoEscolhidoComOrigem escolhido = new(Escolha(processo, "INSCRICAO", origem), origem);
        origem.Desativar();
        ProcessoParaMotivos.SemAFase(processo, "INSCRICAO");

        Assunto(processo, escolhido).Select(c => c.Error.Code).Should().Equal(MotivoDeDecisaoErrorCodes.MotivoDesativado);
        ItensDosMotivosDeDecisao.Avaliar(processo.Declaracao(), new MotivosEscolhidos([escolhido], VersaoQueNasce))
            .Single(i => i.Codigo == "motivos_escolhidos_ativos_no_cadastro").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O motivo desativado de lugar aceito é uma causa só no assunto e pende o item do painel")]
    public void Assunto_MotivoDesativadoEmLugarAceito_TemUmaCausa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoEscolhidoComOrigem escolhido = new(Escolha(processo, "INSCRICAO", origem), origem);
        origem.Desativar();

        Assunto(processo, escolhido).Select(c => c.Error.Code).Should().Equal(MotivoDeDecisaoErrorCodes.MotivoDesativado);
        ItensDosMotivosDeDecisao.Avaliar(processo.Declaracao(), new MotivosEscolhidos([escolhido], VersaoQueNasce))
            .Single(i => i.Codigo == "motivos_escolhidos_ativos_no_cadastro").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "Cada item do painel pende exatamente quando a regra dele tem causa no assunto")]
    public void Painel_UmItemPorRegra_PendeComACausaDoAssunto()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDeDecisao semDeclarar = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivosEscolhidos motivos = new(
            [new MotivoEscolhidoComOrigem(Escolha(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), semDeclarar), semDeclarar)], VersaoQueNasce);

        IReadOnlyList<ItemConformidade> itens = ItensDosMotivosDeDecisao.Avaliar(processo.Declaracao(), motivos);

        itens.Should().OnlyContain(i => i.Dimensao == DimensaoConformidade.Cronograma);
        itens.ToDictionary(i => i.Codigo, i => i.Ok).Should().Equal(new Dictionary<string, bool>
        {
            ["motivos_escolhidos_ativos_no_cadastro"] = true,
            ["motivos_escolhidos_de_tipo_declarado_pelo_lugar"] = false,
            ["motivos_cobrem_as_decisoes_do_processo"] = false,
        });
    }

    [Fact(DisplayName = "O painel mostra os três itens dos motivos conformes quando o assunto não tem causa")]
    public void Painel_SemCausa_TodosConformes()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();

        ItensDosMotivosDeDecisao.Avaliar(processo.Declaracao(), new MotivosEscolhidos([], VersaoQueNasce))
            .Should().HaveCount(3).And.OnlyContain(i => i.Ok);
    }

    private static FieldError[] Assunto(ProcessoSeletivo processo, params MotivoEscolhidoComOrigem[] escolhidos) =>
        Assunto(processo, VersaoQueNasce, escolhidos);

    private static FieldError[] Assunto(ProcessoSeletivo processo, int versaoQueNasce, params MotivoEscolhidoComOrigem[] escolhidos)
    {
        CadastrosDaConfiguracao cadastros = CadastrosDeTeste.Com(motivosEscolhidos: new MotivosEscolhidos(escolhidos, versaoQueNasce));
        ContextoDaConferencia contexto = ContextoDaConferencia.De(processo.Declaracao(), cadastros);

        return [.. CausasQueImpedemCongelar.Assuntos.Single(f => f.Nome == NomeDoAssunto.MotivosDeDecisao).Causas(contexto)];
    }

    private static MotivoDeDecisaoDoProcesso Escolha(ProcessoSeletivo processo, string codigoDaFase, MotivoDeDecisao origem) =>
        Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, codigoDaFase), origem);

    private static MotivoDeDecisaoDoProcesso Escolha(ProcessoSeletivo processo, LugarDaDecisao lugar, MotivoDeDecisao origem) =>
        ProcessoParaMotivos.Escolha(processo, lugar, origem, VersaoQueNasce);
}
