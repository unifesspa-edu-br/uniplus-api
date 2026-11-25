namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.AprovacaoDeTeste;

/// <summary>
/// O circuito de aprovação da proteção de dados do tipo de documento (ADR-0153):
/// o que o deixa pendente, as transições e o que cada recusa protege.
/// </summary>
public sealed class AprovacaoDaProtecaoTests
{
    [Fact(DisplayName = "O tipo de documento nasce pendente, mesmo classificado como sensível")]
    public void Tipo_NasceComoPendente()
    {
        TipoDocumento tipo = NovoTipo(ClassificacaoDeProtecao.Sensivel, HipoteseLegalTratamento.TutelaDaSaude);

        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        tipo.Aprovacao.EstaAprovada.Should().BeFalse();
    }

    [Fact(DisplayName = "Mudar só o nome do tipo aprovado o mantém aprovado")]
    public void Tipo_MudarONome_MantemAprovado()
    {
        TipoDocumento tipo = TipoAprovado();

        Atualizar(tipo, nome: "Comprovante de residência atualizado").IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
    }

    [Theory(DisplayName = "Mudar a classificação, a hipótese legal ou a finalidade do tipo aprovado o devolve a pendente e apaga o registro do ciclo")]
    [InlineData(ClassificacaoDeProtecao.Sensivel, HipoteseLegalTratamento.TutelaDaSaude, Finalidade)]
    [InlineData(ClassificacaoDeProtecao.Pessoal, HipoteseLegalTratamento.ExercicioRegularDeDireitos, Finalidade)]
    [InlineData(ClassificacaoDeProtecao.Pessoal, HipoteseLegalTratamento.CumprimentoObrigacaoLegal, "Outra finalidade.")]
    public void Tipo_MudarAProtecao_VoltaAPendente(ClassificacaoDeProtecao classificacao, HipoteseLegalTratamento hipotese, string finalidade)
    {
        TipoDocumento tipo = TipoAprovado();

        Atualizar(tipo, classificacao: classificacao, hipotese: hipotese, finalidade: finalidade).IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        tipo.Aprovacao.SolicitadaEm.Should().BeNull();
        tipo.Aprovacao.DecididaPor.Should().BeNull();
    }

    [Fact(DisplayName = "Solicitar e aprovar registram quem agiu e quando")]
    public void Tipo_SolicitarEAprovar_RegistramAutorEInstante()
    {
        TipoDocumento tipo = NovoTipo();

        tipo.SolicitarAprovacaoDaProtecao(Isabella, Segunda).IsSuccess.Should().BeTrue();
        tipo.AprovarProtecao(Caroline, Terca).IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Should().BeEquivalentTo(new
        {
            Situacao = SituacaoDaAprovacao.Aprovada,
            SolicitadaPor = Isabella,
            SolicitadaEm = Segunda,
            DecididaPor = Caroline,
            DecididaEm = Terca,
        });
    }

    [Fact(DisplayName = "A decisão sobre o tipo que não está em solicitação é recusada")]
    public void Tipo_DecisaoSemSolicitacao_Recusada()
    {
        TipoDocumento tipo = NovoTipo();

        tipo.AprovarProtecao(Caroline, Terca).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
        tipo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Terca).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
    }

    [Fact(DisplayName = "O ajuste exige a classificação recomendada")]
    public void Tipo_RecomendarAjusteSemRecomendacao_Recusada()
    {
        TipoDocumento tipo = TipoSolicitado();

        Result resultado = tipo.RecomendarAjusteDaProtecao(null, null, Caroline, Terca);

        resultado.Errors.Should().ContainSingle(e => e.Field == "classificacaoRecomendada")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    [Fact(DisplayName = "Aceitar a recomendação aprova o tipo com a classificação indicada, sem nova solicitação")]
    public void Tipo_AceitarARecomendacao_AprovaComAClassificacaoIndicada()
    {
        TipoDocumento tipo = TipoComAjusteRecomendado();

        tipo.AceitarRecomendacaoDaProtecao(Isabella, Quarta).IsSuccess.Should().BeTrue();

        tipo.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Sensivel);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
    }

    [Fact(DisplayName = "O aceite registra a classificação recomendada, quem recomendou o ajuste e quem aceitou, e quando")]
    public void Tipo_AceitarARecomendacao_RegistraQuemRecomendouEQuemAceitou()
    {
        TipoDocumento tipo = TipoComAjusteRecomendado();

        tipo.AceitarRecomendacaoDaProtecao(Isabella, Quarta);

        tipo.Aprovacao.Should().BeEquivalentTo(new
        {
            ClassificacaoRecomendada = ClassificacaoDeProtecao.Sensivel,
            DecididaPor = Caroline,
            AceitaPor = Isabella,
            AceitaEm = Quarta,
        });
    }

    [Fact(DisplayName = "O ajuste recusa a recomendação que a hipótese legal do tipo não admite, para o aceite não ficar sem saída")]
    public void Tipo_RecomendarAjusteComRecomendacaoIncompativelComAHipotese_Recusada()
    {
        TipoDocumento tipo = NovoTipo(ClassificacaoDeProtecao.Pessoal, HipoteseLegalTratamento.InteresseLegitimo);
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Segunda);

        Result resultado = tipo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Terca);

        resultado.Errors.Should().ContainSingle(e => e.Field == "classificacaoRecomendada")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoIncompativelComAHipoteseLegal);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    [Fact(DisplayName = "Recomendar a classificação que o item já tem é decisão inválida: o item se aprova")]
    public void Tipo_RecomendarAjusteComRecomendacaoIgualAAtual_Recusada()
    {
        TipoDocumento tipo = TipoSolicitado();

        Result resultado = tipo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Pessoal, null, Caroline, Terca);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoIgualAAtual);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    [Fact(DisplayName = "Aceitar a recomendação do tipo que não teve ajuste recomendado é recusado")]
    public void Tipo_AceitarSemAjuste_Recusado()
    {
        TipoDocumento tipo = TipoSolicitado();

        tipo.AceitarRecomendacaoDaProtecao(Isabella, Quarta).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemRecomendacaoParaAceitar);
    }

    [Fact(DisplayName = "Editar a classificação do tipo com ajuste recomendado o devolve a pendente")]
    public void Tipo_EditarOTipoComAjusteRecomendado_VoltaAPendente()
    {
        TipoDocumento tipo = TipoComAjusteRecomendado();

        Atualizar(tipo, classificacao: ClassificacaoDeProtecao.Sensivel, hipotese: HipoteseLegalTratamento.TutelaDaSaude).IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        tipo.Aprovacao.ClassificacaoRecomendada.Should().BeNull();
    }

    [Fact(DisplayName = "A devolução registra a justificativa e quem devolveu, sem classificação recomendada")]
    public void Tipo_Devolver_RegistraAJustificativa()
    {
        TipoDocumento tipo = TipoSolicitado();

        tipo.DevolverProtecao("A finalidade não justifica o dado pedido.", Caroline, Terca).IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Should().BeEquivalentTo(new
        {
            Situacao = SituacaoDaAprovacao.Devolvida,
            Justificativa = "A finalidade não justifica o dado pedido.",
            ClassificacaoRecomendada = (ClassificacaoDeProtecao?)null,
            DecididaPor = Caroline,
            DecididaEm = Terca,
        });
        tipo.Aprovacao.EstaAprovada.Should().BeFalse();
    }

    [Fact(DisplayName = "Aceitar a recomendação do item devolvido é recusado com código próprio")]
    public void Tipo_AceitarItemDevolvido_Recusado()
    {
        TipoDocumento tipo = TipoSolicitado();
        tipo.DevolverProtecao("Dado desnecessário.", Caroline, Terca);

        tipo.AceitarRecomendacaoDaProtecao(Isabella, Quarta).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.AceiteDeItemDevolvido);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Devolvida);
    }

    [Fact(DisplayName = "Editar a proteção do tipo devolvido o devolve a pendente e apaga a justificativa")]
    public void Tipo_EditarOTipoDevolvido_VoltaAPendente()
    {
        TipoDocumento tipo = TipoSolicitado();
        tipo.DevolverProtecao("Dado desnecessário.", Caroline, Terca);

        Atualizar(tipo, classificacao: ClassificacaoDeProtecao.Sensivel, hipotese: HipoteseLegalTratamento.TutelaDaSaude).IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        tipo.Aprovacao.Justificativa.Should().BeNull();
    }

    [Fact(DisplayName = "Salvar o tipo com ajuste recomendado ou devolvido o devolve a pendente mesmo sem mudar a proteção")]
    public void Tipo_QualquerEdicaoDevolveAjusteEDevolvidoAPendente()
    {
        TipoDocumento ajustado = TipoComAjusteRecomendado();
        TipoDocumento devolvido = TipoSolicitado();
        devolvido.DevolverProtecao("Dado desnecessário.", Caroline, Terca);

        Atualizar(ajustado, nome: "Outro nome").IsSuccess.Should().BeTrue();
        Atualizar(devolvido, nome: "Outro nome").IsSuccess.Should().BeTrue();

        ajustado.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        ajustado.Aprovacao.Justificativa.Should().BeNull();
        devolvido.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        devolvido.Aprovacao.Justificativa.Should().BeNull();
    }

    [Fact(DisplayName = "A edição recusada não devolve o tipo devolvido a pendente")]
    public void Tipo_EdicaoRecusada_MantemODevolvido()
    {
        TipoDocumento devolvido = TipoSolicitado();
        devolvido.DevolverProtecao("Dado desnecessário.", Caroline, Terca);

        Atualizar(devolvido, nome: " ").IsFailure.Should().BeTrue();

        devolvido.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Devolvida);
    }

    [Theory(DisplayName = "A devolução sem justificativa, em branco ou com mais de 2.000 caracteres é recusada no campo")]
    [InlineData(null, "ProtecaoDoCatalogo.JustificativaObrigatoria")]
    [InlineData("   ", "ProtecaoDoCatalogo.JustificativaObrigatoria")]
    [InlineData("TAMANHO", "ProtecaoDoCatalogo.JustificativaMuitoLonga")]
    public void Tipo_DevolverComJustificativaInvalida_Recusa(string? justificativa, string codigo)
    {
        TipoDocumento tipo = TipoSolicitado();
        string? enviada = justificativa == "TAMANHO" ? new string('a', 2001) : justificativa;

        Result resultado = tipo.DevolverProtecao(enviada, Caroline, Terca);

        resultado.Errors.Should().ContainSingle(e => e.Field == "justificativa").Which.Error.Code.Should().Be(codigo);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    [Fact(DisplayName = "O ajuste com mais de 2.000 caracteres é recusado, e o de exatamente 2.000 é aceito aparado")]
    public void Tipo_AjusteComJustificativaNoLimite()
    {
        TipoDocumento longo = TipoSolicitado();
        TipoDocumento nolimite = TipoSolicitado();

        Result recusado = longo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, new string('a', 2001), Caroline, Terca);
        Result aceito = nolimite.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, " " + new string('a', 2000) + " ", Caroline, Terca);

        recusado.Errors.Should().ContainSingle(e => e.Field == "justificativa")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.JustificativaMuitoLonga);
        aceito.IsSuccess.Should().BeTrue();
        nolimite.Aprovacao.Justificativa.Should().HaveLength(2000);
    }

    private static TipoDocumento NovoTipo(
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        HipoteseLegalTratamento hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal) =>
        TipoDocumento.Criar(new DeclaracaoDoTipoDocumento(
            "COMPROVANTE_RESIDENCIA", "Comprovante de residência", null, "ENDERECO", null, null, null, classificacao, Finalidade, hipotese)).Value!;

    private static TipoDocumento TipoSolicitado()
    {
        TipoDocumento tipo = NovoTipo();
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        return tipo;
    }

    private static TipoDocumento TipoComAjusteRecomendado()
    {
        TipoDocumento tipo = NovoTipo(ClassificacaoDeProtecao.Interno, HipoteseLegalTratamento.CumprimentoObrigacaoLegal);
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        tipo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Terca);
        return tipo;
    }

    private static TipoDocumento TipoAprovado()
    {
        TipoDocumento tipo = TipoSolicitado();
        tipo.AprovarProtecao(Caroline, Terca);
        return tipo;
    }

    private static Result Atualizar(
        TipoDocumento tipo,
        string nome = "Comprovante de residência",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        HipoteseLegalTratamento hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal,
        string finalidade = Finalidade) =>
        tipo.Atualizar(new DeclaracaoDoTipoDocumento("COMPROVANTE_RESIDENCIA", nome, null, "ENDERECO", null, null, null, classificacao, finalidade, hipotese));
}
