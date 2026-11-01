namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O circuito de aprovação da proteção de dados do catálogo (ADR-0153), no tipo de documento e no fato:
/// o que deixa o item pendente, as transições e o que cada recusa protege.
/// </summary>
public sealed class AprovacaoDaProtecaoTests
{
    private const string Isabella = "isabella";
    private const string Caroline = "caroline";
    private const string Finalidade = "Comprovação documental do candidato.";
    private static readonly DateTimeOffset Segunda = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Terca = Segunda.AddDays(1);
    private static readonly DateTimeOffset Quarta = Segunda.AddDays(2);

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
        tipo.NegarProtecao(ClassificacaoDeProtecao.Sensivel, Caroline, Terca).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
    }

    [Fact(DisplayName = "A negativa exige a classificação recomendada")]
    public void Tipo_NegarSemRecomendacao_Recusada()
    {
        TipoDocumento tipo = TipoSolicitado();

        Result resultado = tipo.NegarProtecao(ClassificacaoDeProtecao.Nenhuma, Caroline, Terca);

        resultado.Errors.Should().ContainSingle(e => e.Field == "classificacaoRecomendada")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    [Fact(DisplayName = "Aceitar a recomendação aprova o tipo com a classificação indicada, sem nova solicitação")]
    public void Tipo_AceitarARecomendacao_AprovaComAClassificacaoIndicada()
    {
        TipoDocumento tipo = NovoTipo(ClassificacaoDeProtecao.Interno, HipoteseLegalTratamento.CumprimentoObrigacaoLegal);
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        tipo.NegarProtecao(ClassificacaoDeProtecao.Sensivel, Caroline, Terca);

        tipo.AceitarRecomendacaoDaProtecao(Isabella, Quarta).IsSuccess.Should().BeTrue();

        tipo.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Sensivel);
        tipo.Aprovacao.Should().BeEquivalentTo(new
        {
            Situacao = SituacaoDaAprovacao.Aprovada,
            ClassificacaoRecomendada = ClassificacaoDeProtecao.Sensivel,
            DecididaPor = Caroline,
            AceitaPor = Isabella,
            AceitaEm = Quarta,
        });
    }

    [Fact(DisplayName = "Aceitar a recomendação que a hipótese legal do tipo não comporta é recusado e o tipo continua negado")]
    public void Tipo_AceitarRecomendacaoIncompativelComAHipotese_Recusado()
    {
        TipoDocumento tipo = NovoTipo(ClassificacaoDeProtecao.Pessoal, HipoteseLegalTratamento.InteresseLegitimo);
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        tipo.NegarProtecao(ClassificacaoDeProtecao.Sensivel, Caroline, Terca);

        Result resultado = tipo.AceitarRecomendacaoDaProtecao(Isabella, Quarta);

        resultado.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoIncompativelComAHipoteseLegal);
        tipo.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Pessoal);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Negada);
    }

    [Fact(DisplayName = "Aceitar a recomendação do tipo que não foi negado é recusado")]
    public void Tipo_AceitarSemNegativa_Recusado()
    {
        TipoDocumento tipo = TipoSolicitado();

        tipo.AceitarRecomendacaoDaProtecao(Isabella, Quarta).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.SemRecomendacaoParaAceitar);
    }

    [Fact(DisplayName = "Editar a classificação do tipo negado o devolve a pendente")]
    public void Tipo_EditarOTipoNegado_VoltaAPendente()
    {
        TipoDocumento tipo = TipoSolicitado();
        tipo.NegarProtecao(ClassificacaoDeProtecao.Sensivel, Caroline, Terca);

        Atualizar(tipo, classificacao: ClassificacaoDeProtecao.Sensivel, hipotese: HipoteseLegalTratamento.TutelaDaSaude).IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        tipo.Aprovacao.ClassificacaoRecomendada.Should().BeNull();
    }

    [Fact(DisplayName = "O fato do administrador nasce pendente")]
    public void Fato_NasceComoPendente() =>
        NovoFato().Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);

    [Fact(DisplayName = "Mudar a classificação do fato aprovado o devolve a pendente; mudar só o nome não")]
    public void Fato_SoAProtecaoReiniciaOCircuito()
    {
        FatoCandidato fato = FatoAprovado(NovoFato());

        fato.AlterarDescritivo("Outro nome", null).IsSuccess.Should().BeTrue();
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);

        fato.AlterarDefinicao(Definicao(fato, classificacao: ClassificacaoDeProtecao.Sensivel, hipotese: HipoteseLegalTratamento.TutelaDaSaude), Catalogo(fato))
            .IsSuccess.Should().BeTrue();
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
    }

    [Fact(DisplayName = "No fato de sistema a hipótese legal se edita e o deixa pendente")]
    public void FatoDeSistema_MudarAHipoteseLegal_VoltaAPendente()
    {
        FatoCandidato fato = FatoAprovado(NovoFatoDeSistema());

        Result resultado = fato.AlterarDefinicao(Definicao(fato, hipotese: HipoteseLegalTratamento.ExercicioRegularDeDireitos), Catalogo(fato));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.HipoteseLegal.Should().Be(HipoteseLegalTratamento.ExercicioRegularDeDireitos);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
    }

    [Fact(DisplayName = "No fato de sistema a definição continua travada e o fato não muda")]
    public void FatoDeSistema_MudarODominio_RecusadoSemMutar()
    {
        FatoCandidato fato = FatoAprovado(NovoFatoDeSistema());

        Result resultado = fato.AlterarDefinicao(
            Definicao(fato, dominio: DominioFato.Booleano, classificacao: ClassificacaoDeProtecao.Sensivel, hipotese: HipoteseLegalTratamento.TutelaDaSaude), Catalogo(fato));

        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);
        resultado.Error.Message.Should().Contain("domínio").And.Contain("travados");
        fato.Dominio.Should().Be(DominioFato.Categorico);
        fato.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Pessoal);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
    }

    [Fact(DisplayName = "Aceitar a recomendação vale para o fato de sistema e não muda o resto da definição")]
    public void FatoDeSistema_AceitarARecomendacao_AprovaComAClassificacaoIndicada()
    {
        FatoCandidato fato = NovoFatoDeSistema();
        fato.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        fato.NegarProtecao(ClassificacaoDeProtecao.Sensivel, Caroline, Terca).IsSuccess.Should().BeTrue();

        Result resultado = fato.AceitarRecomendacaoDaProtecao(Isabella, Quarta, Catalogo(fato));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.Should().BeEquivalentTo(new
        {
            ClassificacaoProtecao = ClassificacaoDeProtecao.Sensivel,
            Codigo = "COR_RACA",
            Dominio = DominioFato.Categorico,
            Sistema = true,
        });
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
        fato.Aprovacao.AceitaPor.Should().Be(Isabella);
    }

    [Fact(DisplayName = "A negativa não recomenda classificação abaixo do mínimo do domínio do fato")]
    public void Fato_NegarAbaixoDoMinimoDoDominio_Recusado()
    {
        FatoCandidato fato = NovoFato(DominioFato.Texto);
        fato.SolicitarAprovacaoDaProtecao(Isabella, Segunda);

        Result resultado = fato.NegarProtecao(ClassificacaoDeProtecao.Publico, Caroline, Terca);

        resultado.Errors.Should().ContainSingle(e => e.Field == "classificacaoRecomendada")
            .Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    private static TipoDocumento NovoTipo(
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        HipoteseLegalTratamento hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal) =>
        TipoDocumento.Criar(
            "COMPROVANTE_RESIDENCIA", "Comprovante de residência", null, "ENDERECO", null, null, null, classificacao, Finalidade, hipotese).Value!;

    private static TipoDocumento TipoSolicitado()
    {
        TipoDocumento tipo = NovoTipo();
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
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
        tipo.Atualizar("COMPROVANTE_RESIDENCIA", nome, null, "ENDERECO", null, null, null, classificacao, finalidade, hipotese);

    private static FatoCandidato NovoFato(DominioFato dominio = DominioFato.Booleano) =>
        FatoCandidato.CriarDoAdministrador(
            "TEM_RENDA", "Tem renda", null, dominio, CardinalidadeFato.Escalar, null, dominio == DominioFato.Texto ? FormatoTexto.Livre : null,
            "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

    private static FatoCandidato NovoFatoDeSistema() =>
        FatoCandidato.Criar(
            "COR_RACA", "Cor ou raça", null, DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            null, "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade,
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal, sistema: true).Value!;

    private static FatoCandidato FatoAprovado(FatoCandidato fato)
    {
        fato.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        fato.AprovarProtecao(Caroline, Terca);
        return fato;
    }

    private static DefinicaoDoFato Definicao(
        FatoCandidato fato,
        DominioFato? dominio = null,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        HipoteseLegalTratamento hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal) =>
        new(
            dominio ?? fato.Dominio, fato.Cardinalidade, fato.FonteValores, fato.Formato, fato.PontoResolucao, fato.Escopo,
            classificacao, Finalidade, hipotese);

    private static CatalogoDeFatos Catalogo(params FatoCandidato[] fatos) => new(fatos, []);
}
