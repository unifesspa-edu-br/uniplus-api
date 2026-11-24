namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os motivos de decisão quando a leitura do documento recusou parte do que declara os tipos de decisão: a fase ou a etapa
/// inteira, ou a lista dos tipos de uma delas. Não se sabe então quais lugares declaram o tipo, e nem a recusa por omissão
/// nem a aprovação por omissão são aceitas.
/// </summary>
public sealed class MotivosComDeclaracaoDosTiposRecusadaTests
{
    private const int VersaoQueNasce = 1;

    [Fact(DisplayName = "Sem a recusa na leitura, o fundamento sem lugar que declare o tipo é recusado")]
    public void Controle_SemParteRecusada_RecusaOTipoNaoDeclarado()
    {
        ProcessoSeletivo processo = ProcessoSemLugarQueDeclareAIsencao();

        Causas(processo, OrigemNoDocumento.Identidade).Select(c => c.Error.Code).Should().Equal(MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
    }

    [Theory(DisplayName = "Com parte recusada no que declara os tipos, o fundamento sem lugar que declare o tipo não é recusado")]
    [InlineData("fases")]
    [InlineData("etapas")]
    [InlineData("fases[0].tiposDeDecisao")]
    [InlineData("etapas[1].tiposDeDecisao")]
    public void FundamentoSemLugar_ComParteRecusadaNosTipos_NaoRecusa(string colecaoRecusada)
    {
        ProcessoSeletivo processo = ProcessoSemLugarQueDeclareAIsencao();

        Causas(processo, DeclaracoesAdotadasDeTeste.ComPartesRecusadas(colecaoRecusada)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A recusa de parte de outra lista da fase não suspende a recusa do tipo não declarado")]
    public void FundamentoSemLugar_ComParteRecusadaEmOutraLista_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemLugarQueDeclareAIsencao();

        Causas(processo, DeclaracoesAdotadasDeTeste.ComPartesRecusadas("fases[0].produtos", "etapas[0].bancas"))
            .Select(c => c.Error.Code).Should().Equal(MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
    }

    [Fact(DisplayName = "O lugar que aceitou o tipo na lista parcialmente recusada continua exigindo a cobertura")]
    public void LugarQueAceitouOTipo_ComListaDosTiposRecusada_ContinuaExigindoCobertura()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho("CADASTRO_UNICO");

        Causas(processo, DeclaracoesAdotadasDeTeste.ComPartesRecusadas("fases[0].tiposDeDecisao"))
            .Select(c => c.Error.Code).Should().Equal(MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
    }

    [Fact(DisplayName = "O motivo de tipo que não está entre os aceitos não é recusado no lugar com a lista dos tipos parcialmente recusada")]
    public void MotivoDeTipoNaoAceito_ComListaDosTiposRecusada_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao homologacao = ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "DOCUMENTO_ILEGIVEL", SentidoDoMotivo.Favoravel);

        Causas(processo, DeclaracoesAdotadasDeTeste.ComPartesRecusadas("fases[0].tiposDeDecisao"), NaInscricao(processo, homologacao)).Should().BeEmpty();
        Causas(processo, OrigemNoDocumento.Identidade, NaInscricao(processo, homologacao))
            .Select(c => c.Error.Code).Should().Equal(MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
    }

    [Theory(DisplayName = "O painel deixa pendentes os itens que dependem do que declara os tipos, e só eles, quando a leitura recusou parte dele")]
    [InlineData("fases")]
    [InlineData("etapas")]
    [InlineData("fases[0].tiposDeDecisao")]
    [InlineData("etapas[1].tiposDeDecisao")]
    public void Painel_ComParteRecusadaNosTipos_DeixaPendentesOsItensQueDependemDeles(string colecaoRecusada)
    {
        ProcessoSeletivo processo = ProcessoSemLugarQueDeclareAIsencao();

        IReadOnlyList<ItemConformidade> itens = ItensDosMotivosDeDecisao.Avaliar(
            Lida(processo, DeclaracoesAdotadasDeTeste.ComPartesRecusadas(colecaoRecusada)), new MotivosEscolhidos([], VersaoQueNasce));

        itens.ToDictionary(i => i.Codigo, i => i.Ok).Should().Equal(new Dictionary<string, bool>
        {
            ["motivos_escolhidos_ativos_no_cadastro"] = true,
            ["motivos_escolhidos_de_tipo_declarado_pelo_lugar"] = false,
            ["motivos_cobrem_as_decisoes_do_processo"] = false,
        });
    }

    [Fact(DisplayName = "O painel aprova os itens dos tipos quando nada foi recusado e nenhum motivo foi escolhido")]
    public void Painel_SemParteRecusada_AprovaOsItensDosTipos()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();

        ItensDosMotivosDeDecisao.Avaliar(Lida(processo, OrigemNoDocumento.Identidade), new MotivosEscolhidos([], VersaoQueNasce))
            .Should().OnlyContain(i => i.Ok);
    }

    private static ProcessoSeletivo ProcessoSemLugarQueDeclareAIsencao()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho("CADASTRO_UNICO");
        ProcessoParaMotivos.DeclararNaFase(processo, "INSCRICAO");

        return processo;
    }

    private static MotivoEscolhidoComOrigem NaInscricao(ProcessoSeletivo processo, MotivoDeDecisao origem) =>
        new(ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), origem, VersaoQueNasce), origem);

    private static FieldError[] Causas(ProcessoSeletivo processo, OrigemNoDocumento origem, params MotivoEscolhidoComOrigem[] escolhidos)
    {
        CadastrosDaConfiguracao cadastros = CadastrosDeTeste.Com(motivosEscolhidos: new MotivosEscolhidos(escolhidos, VersaoQueNasce));
        ContextoDaConferencia contexto = ContextoDaConferencia.De(Lida(processo, origem), cadastros);

        return [.. CausasQueImpedemCongelar.Assuntos.Single(f => f.Nome == NomeDoAssunto.MotivosDeDecisao).Causas(contexto)];
    }

    private static ConfiguracaoDeclarada Lida(ProcessoSeletivo processo, OrigemNoDocumento origem)
    {
        ConfiguracaoDeclarada raiz = processo.Declaracao();

        return new ConfiguracaoDeclarada(raiz.Identidade, raiz.Cronograma, raiz.Avaliacao, raiz.Vagas, raiz.Exigencias, raiz.Coleta, raiz.DivulgacaoETaxa)
        {
            Origem = origem,
        };
    }
}
