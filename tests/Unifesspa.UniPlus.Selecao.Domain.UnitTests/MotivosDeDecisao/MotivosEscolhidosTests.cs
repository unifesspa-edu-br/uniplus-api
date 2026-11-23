namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Os motivos escolhidos lidos para a versão que nasce: o desativado no cadastro (UNI-REQ-0122), o de tipo que a fase ou a
/// etapa não declara e o que vale para a cobertura.
/// </summary>
public sealed class MotivosEscolhidosTests
{
    private const int VersaoQueNasce = 2;

    [Fact(DisplayName = "O motivo pendente cujo motivo de origem foi desativado é recusado no lugar e no código dele")]
    public void Desativados_Pendente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "RENDA_ACIMA_DO_LIMITE");
        MotivoEscolhidoComOrigem escolhido = NaInscricao(processo, origem, versaoDeEntrada: VersaoQueNasce);
        origem.Desativar();

        FieldError recusa = OsMotivos(escolhido).Desativados(ProcessoParaMotivos.Lugares(processo)).Should().ContainSingle().Subject;

        recusa.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoDesativado);
        recusa.Field.Should().Be("motivosDeDecisao");
        recusa.Error.Message.Should().Be(
            "A fase 'INSCRICAO' tem o motivo RENDA_ACIMA_DO_LIMITE, desativado no cadastro depois de escolhido; troque-o antes de publicar.");
    }

    [Fact(DisplayName = "O motivo pendente cujo motivo de origem não existe mais no cadastro é recusado como desativado")]
    public void Desativados_SemOrigem_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisaoDoProcesso escolhido = Escolha(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel), VersaoQueNasce);

        OsMotivos(new MotivoEscolhidoComOrigem(escolhido, null)).Desativados(ProcessoParaMotivos.Lugares(processo))
            .Should().ContainSingle().Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoDesativado);
    }

    [Fact(DisplayName = "O motivo que já consta de versão publicada não é recusado quando o motivo de origem é desativado depois")]
    public void Desativados_JaPublicado_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoEscolhidoComOrigem publicado = NaInscricao(processo, origem, versaoDeEntrada: 1);
        origem.Desativar();

        OsMotivos(publicado).Desativados(ProcessoParaMotivos.Lugares(processo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo ativo no cadastro não é recusado")]
    public void Desativados_Ativo_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoEscolhidoComOrigem ativo = NaInscricao(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel), VersaoQueNasce);

        OsMotivos(ativo).Desativados(ProcessoParaMotivos.Lugares(processo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas saem pelos lugares, fases antes das etapas, e dentro do lugar pelo código em ordem ordinal")]
    public void Recusas_NaOrdemDosLugaresEDoCodigo()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();

        IEnumerable<FieldError> recusas = OsMotivos(DesativadosEmVariosLugares(processo)).Desativados(ProcessoParaMotivos.Lugares(processo));

        recusas.Select(static r => CodigoCitado(r.Error.Message)).Should().Equal(
            "A_FASE_DA_INSCRICAO", "Z_FASE_DA_INSCRICAO", "B_FASE_DA_HOMOLOGACAO",
            "A_ETAPA_DA_OBJETIVA", "A_ETAPA_DA_DISCURSIVA");
    }

    private static string CodigoCitado(string mensagem) =>
        mensagem.Split(' ', StringSplitOptions.RemoveEmptyEntries).Single(static p => p.Contains("_DA_", StringComparison.Ordinal)).TrimEnd(',');

    private static MotivoEscolhidoComOrigem[] DesativadosEmVariosLugares(ProcessoSeletivo processo) =>
    [
        EmDesativado(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova discursiva"), "A_ETAPA_DA_DISCURSIVA"),
        EmDesativado(processo, ProcessoParaMotivos.LugarDaFase(processo, "HOMOLOGACAO_INSCRICOES"), "B_FASE_DA_HOMOLOGACAO"),
        EmDesativado(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), "Z_FASE_DA_INSCRICAO"),
        EmDesativado(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), "A_FASE_DA_INSCRICAO"),
        EmDesativado(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), "A_ETAPA_DA_OBJETIVA"),
    ];

    [Fact(DisplayName = "O motivo pendente desativado de fase que o retrato não traz é recusado sem nomear o lugar")]
    public void Desativados_FaseForaDoRetrato_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoEscolhidoComOrigem escolhido = EmDesativado(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), "RENDA_ACIMA_DO_LIMITE");
        ProcessoParaMotivos.SemAFase(processo, "INSCRICAO");

        FieldError recusa = OsMotivos(escolhido).Desativados(ProcessoParaMotivos.Lugares(processo)).Should().ContainSingle().Subject;

        recusa.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoDesativado);
        recusa.Field.Should().Be("motivosDeDecisao");
        recusa.Error.Message.Should().StartWith("O motivo RENDA_ACIMA_DO_LIMITE está desativado no cadastro depois de escolhido");
    }

    [Fact(DisplayName = "O motivo pendente desativado de etapa que o retrato não traz é recusado")]
    public void Desativados_EtapaForaDoRetrato_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoEscolhidoComOrigem escolhido = EmDesativado(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova discursiva"), "A_ETAPA_DA_DISCURSIVA");
        ProcessoParaMotivos.SemAEtapa(processo, "Prova discursiva");

        OsMotivos(escolhido).Desativados(ProcessoParaMotivos.Lugares(processo))
            .Should().ContainSingle().Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoDesativado);
    }

    [Fact(DisplayName = "O motivo já publicado desativado depois não é recusado nem fora do retrato")]
    public void Desativados_PublicadoForaDoRetrato_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = IsencaoFavoravel();
        MotivoEscolhidoComOrigem publicado = NaInscricao(processo, origem, versaoDeEntrada: 1);
        origem.Desativar();
        ProcessoParaMotivos.SemAFase(processo, "INSCRICAO");

        OsMotivos(publicado).Desativados(ProcessoParaMotivos.Lugares(processo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo desativado de lugar aceito é recusado uma vez só e vem antes do que está fora do retrato")]
    public void Desativados_LugarAceitoEForaDoRetrato_NaoDuplicaEVemPrimeiro()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoEscolhidoComOrigem fora = EmDesativado(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova discursiva"), "A_FORA");
        MotivoEscolhidoComOrigem aceito = EmDesativado(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), "Z_ACEITO");
        ProcessoParaMotivos.SemAEtapa(processo, "Prova discursiva");

        IEnumerable<FieldError> recusas = OsMotivos(fora, aceito).Desativados(ProcessoParaMotivos.Lugares(processo));

        recusas.Select(static r => r.Error.Message).Should().SatisfyRespectively(
            primeira => primeira.Should().Contain("Z_ACEITO").And.StartWith("A fase 'INSCRICAO'"),
            segunda => segunda.Should().Contain("A_FORA").And.StartWith("O motivo A_FORA"));
    }

    [Fact(DisplayName = "O motivo pendente de tipo que a etapa não declara é recusado pedindo para declarar o tipo ou retirar o motivo")]
    public void DeTipoNaoDeclarado_Pendente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoEscolhidoComOrigem naEtapa = Na(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), IsencaoFavoravel(), VersaoQueNasce);

        FieldError recusa = OsMotivos(naEtapa).DeTipoNaoDeclarado(ProcessoParaMotivos.Lugares(processo)).Should().ContainSingle().Subject;

        recusa.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
        recusa.Field.Should().Be("motivosDeDecisao");
        recusa.Error.Message.Should().Be(
            "A etapa 'Prova objetiva' não declara o tipo de decisão ISENCAO_DA_TAXA do motivo RENDA_ACIMA_DO_LIMITE; declare o tipo ou retire o motivo.");
    }

    [Fact(DisplayName = "O motivo já publicado de tipo que a fase deixou de declarar é recusado pedindo para declarar o tipo de novo, porque a retirada é recusada")]
    public void DeTipoNaoDeclarado_Publicado_PedeParaDeclararDeNovo()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoEscolhidoComOrigem publicado = NaInscricao(processo, IsencaoFavoravel(), versaoDeEntrada: 1);
        ProcessoParaMotivos.DeclararNaFase(processo, "INSCRICAO", TipoMotivoDeDecisao.HomologacaoDasInscricoes);

        FieldError recusa = OsMotivos(publicado).DeTipoNaoDeclarado(ProcessoParaMotivos.Lugares(processo)).Should().ContainSingle().Subject;

        recusa.Error.Message.Should().Be(
            "A fase 'INSCRICAO' não declara o tipo de decisão ISENCAO_DA_TAXA do motivo RENDA_ACIMA_DO_LIMITE, que já consta do edital publicado e não pode ser retirado; declare o tipo de novo.");
    }

    [Fact(DisplayName = "O motivo de tipo que o lugar declara não é recusado")]
    public void DeTipoNaoDeclarado_Declarado_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();

        OsMotivos(NaInscricao(processo, IsencaoFavoravel(), VersaoQueNasce)).DeTipoNaoDeclarado(ProcessoParaMotivos.Lugares(processo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A versão leva o motivo ativo de tipo declarado e o já publicado desativado depois, e não leva o pendente desativado")]
    public void QueCompoemAVersao_SemOsPendentesDesativados()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao antiga = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "ANTIGO");
        MotivoDeDecisao desativada = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "DESATIVADO");
        MotivoDeDecisao ativa = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "ATIVO");
        MotivoEscolhidoComOrigem[] escolhidos =
        [
            NaInscricao(processo, antiga, versaoDeEntrada: 1), NaInscricao(processo, desativada, VersaoQueNasce), NaInscricao(processo, ativa, VersaoQueNasce),
        ];
        antiga.Desativar();
        desativada.Desativar();

        OsMotivos(escolhidos).QueCompoemAVersao(ProcessoParaMotivos.Lugares(processo)).Select(m => m.Codigo.Valor).Should().BeEquivalentTo(["ANTIGO", "ATIVO"]);
    }

    [Fact(DisplayName = "A versão não leva para a cobertura o motivo de tipo que o lugar não declara, nem o já publicado")]
    public void QueCompoemAVersao_SemOsDeTipoNaoDeclarado()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoEscolhidoComOrigem[] escolhidos =
        [
            Na(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), IsencaoFavoravel(), VersaoQueNasce),
            NaInscricao(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "PUBLICADO"), versaoDeEntrada: 1),
            NaInscricao(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "DECLARADO"), VersaoQueNasce),
        ];
        ProcessoParaMotivos.DeclararNaFase(processo, "INSCRICAO", TipoMotivoDeDecisao.IsencaoDaTaxa);

        OsMotivos(escolhidos).QueCompoemAVersao(ProcessoParaMotivos.Lugares(processo)).Select(m => m.Codigo.Valor).Should().BeEquivalentTo(["PUBLICADO", "DECLARADO"]);
    }

    [Fact(DisplayName = "O motivo desativado de tipo não declarado aparece nas duas recusas")]
    public void Recusas_AcumulamQuandoOMotivoQuebraDuasRegras()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = IsencaoFavoravel();
        MotivoEscolhidoComOrigem escolhido = Na(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), origem, VersaoQueNasce);
        origem.Desativar();
        LugaresDaDecisao lugares = ProcessoParaMotivos.Lugares(processo);

        MotivosEscolhidos motivos = OsMotivos(escolhido);

        motivos.Desativados(lugares).Should().ContainSingle();
        motivos.DeTipoNaoDeclarado(lugares).Should().ContainSingle();
        motivos.QueCompoemAVersao(lugares).Should().BeEmpty();
    }

    private static MotivosEscolhidos OsMotivos(params MotivoEscolhidoComOrigem[] escolhidos) => new(escolhidos, VersaoQueNasce);

    private static MotivoDeDecisao IsencaoFavoravel() =>
        ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);

    private static MotivoDeDecisaoDoProcesso Escolha(ProcessoSeletivo processo, MotivoDeDecisao origem, int versaoDeEntrada) =>
        ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), origem, versaoDeEntrada);

    private static MotivoEscolhidoComOrigem NaInscricao(ProcessoSeletivo processo, MotivoDeDecisao origem, int versaoDeEntrada) =>
        Na(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), origem, versaoDeEntrada);

    private static MotivoEscolhidoComOrigem Na(ProcessoSeletivo processo, LugarDaDecisao lugar, MotivoDeDecisao origem, int versaoDeEntrada) =>
        new(ProcessoParaMotivos.Escolha(processo, lugar, origem, versaoDeEntrada), origem);

    // O tipo é de isenção da taxa em qualquer lugar: só o código distingue as recusas na ordem esperada.
    private static MotivoEscolhidoComOrigem EmDesativado(ProcessoSeletivo processo, LugarDaDecisao lugar, string codigo)
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, codigo);
        MotivoEscolhidoComOrigem escolhido = Na(processo, lugar, origem, VersaoQueNasce);
        origem.Desativar();

        return escolhido;
    }
}
