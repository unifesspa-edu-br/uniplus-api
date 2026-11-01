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
        tipo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Terca).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
    }

    [Fact(DisplayName = "O ajuste exige a classificação recomendada")]
    public void Tipo_RecomendarAjusteSemRecomendacao_Recusada()
    {
        TipoDocumento tipo = TipoSolicitado();

        Result resultado = tipo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Nenhuma, null, Caroline, Terca);

        resultado.Errors.Should().ContainSingle(e => e.Field == "classificacaoRecomendada")
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    [Fact(DisplayName = "Aceitar a recomendação aprova o tipo com a classificação indicada, sem nova solicitação")]
    public void Tipo_AceitarARecomendacao_AprovaComAClassificacaoIndicada()
    {
        TipoDocumento tipo = NovoTipo(ClassificacaoDeProtecao.Interno, HipoteseLegalTratamento.CumprimentoObrigacaoLegal);
        tipo.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        tipo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Terca);

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

    [Fact(DisplayName = "O ajuste do fato recusa a recomendação igual à atual e a que a hipótese legal não admite")]
    public void Fato_RecomendarAjusteComRecomendacaoInvalida_Recusada()
    {
        FatoCandidato igual = NovoFato();
        igual.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        FatoCandidato incompativel = FatoCandidato.CriarDoAdministrador(
            "TEM_CONTRATO", "Tem contrato", "Descrição do fato.", DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.ExecucaoContrato).Value!;
        incompativel.SolicitarAprovacaoDaProtecao(Isabella, Segunda);

        igual.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Pessoal, null, Caroline, Terca).Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoIgualAAtual);
        incompativel.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Terca).Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoIncompativelComAHipoteseLegal);
        incompativel.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    [Fact(DisplayName = "No fato de sistema o domínio, a cardinalidade, o escopo, a fonte ou o formato fora do vocabulário saem como erro de vocabulário no campo, e não como definição travada")]
    public void FatoDeSistema_TokenForaDoVocabulario_ErroDeVocabularioNoCampo()
    {
        FatoCandidato fato = FatoAprovado(NovoFatoDeSistema());
        DefinicaoDoFato invalida = Definicao(fato) with
        {
            Dominio = (DominioFato)(-1),
            Cardinalidade = (CardinalidadeFato)(-1),
            Escopo = (EscopoFato)(-1),
            FonteValores = (FonteValoresFato)(-1),
            Formato = (FormatoTexto)(-1),
        };

        Result resultado = fato.AlterarDefinicao(invalida, Catalogo(fato));

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo("dominio", "cardinalidade", "escopo", "fonteValores", "formato");
        resultado.Errors.Should().NotContain(e => e.Error.Code == FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
    }

    [Fact(DisplayName = "No fato de sistema o ponto de resolução não informado não conta como troca; o diferente é recusado")]
    public void FatoDeSistema_PontoDeResolucaoNaoInformado_NaoRecusa()
    {
        FatoCandidato fato = FatoAprovado(NovoFatoDeSistema());

        Result semPonto = fato.AlterarDefinicao(
            Definicao(fato, hipotese: HipoteseLegalTratamento.ExercicioRegularDeDireitos) with { PontoResolucao = null! }, Catalogo(fato));
        Result outroPonto = fato.AlterarDefinicao(Definicao(fato) with { PontoResolucao = "HABILITACAO" }, Catalogo(fato));

        semPonto.IsSuccess.Should().BeTrue(semPonto.Error?.Message);
        fato.HipoteseLegal.Should().Be(HipoteseLegalTratamento.ExercicioRegularDeDireitos);
        outroPonto.Error!.Code.Should().Be(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);
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
        TipoDocumento tipo = TipoSolicitado();
        tipo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Terca);

        Atualizar(tipo, classificacao: ClassificacaoDeProtecao.Sensivel, hipotese: HipoteseLegalTratamento.TutelaDaSaude).IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        tipo.Aprovacao.ClassificacaoRecomendada.Should().BeNull();
    }

    [Fact(DisplayName = "A reprovação registra a justificativa e quem reprovou, sem classificação recomendada")]
    public void Tipo_Reprovar_RegistraAJustificativa()
    {
        TipoDocumento tipo = TipoSolicitado();

        tipo.ReprovarProtecao("A finalidade não justifica o dado pedido.", Caroline, Terca).IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Should().BeEquivalentTo(new
        {
            Situacao = SituacaoDaAprovacao.Reprovada,
            Justificativa = "A finalidade não justifica o dado pedido.",
            ClassificacaoRecomendada = (ClassificacaoDeProtecao?)null,
            DecididaPor = Caroline,
            DecididaEm = Terca,
        });
        tipo.Aprovacao.EstaAprovada.Should().BeFalse();
    }

    [Fact(DisplayName = "Aceitar a recomendação do item reprovado é recusado com código próprio")]
    public void Tipo_AceitarItemReprovado_Recusado()
    {
        TipoDocumento tipo = TipoSolicitado();
        tipo.ReprovarProtecao("Dado desnecessário.", Caroline, Terca);

        tipo.AceitarRecomendacaoDaProtecao(Isabella, Quarta).Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.AceiteDeItemReprovado);
        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Reprovada);
    }

    [Fact(DisplayName = "Editar a proteção do tipo reprovado o devolve a pendente e apaga a justificativa")]
    public void Tipo_EditarOTipoReprovado_VoltaAPendente()
    {
        TipoDocumento tipo = TipoSolicitado();
        tipo.ReprovarProtecao("Dado desnecessário.", Caroline, Terca);

        Atualizar(tipo, classificacao: ClassificacaoDeProtecao.Sensivel, hipotese: HipoteseLegalTratamento.TutelaDaSaude).IsSuccess.Should().BeTrue();

        tipo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        tipo.Aprovacao.Justificativa.Should().BeNull();
    }

    [Fact(DisplayName = "O fato fora de solicitação não é reprovado")]
    public void Fato_ReprovarSemSolicitacao_Recusado()
    {
        NovoFato().ReprovarProtecao("Dado desnecessário.", Caroline, Terca).Error!.Code
            .Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
    }

    [Fact(DisplayName = "Salvar o tipo com ajuste recomendado ou reprovado o devolve a pendente mesmo sem mudar a proteção; o aprovado continua aprovado")]
    public void Tipo_QualquerEdicaoDevolveAjusteEReprovadoAPendente()
    {
        TipoDocumento ajustado = TipoSolicitado();
        ajustado.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, "Traz dado de saúde.", Caroline, Terca);
        TipoDocumento reprovado = TipoSolicitado();
        reprovado.ReprovarProtecao("Dado desnecessário.", Caroline, Terca);
        TipoDocumento aprovado = TipoAprovado();

        Atualizar(ajustado, nome: "Outro nome").IsSuccess.Should().BeTrue();
        Atualizar(reprovado, nome: "Outro nome").IsSuccess.Should().BeTrue();
        Atualizar(aprovado, nome: "Outro nome").IsSuccess.Should().BeTrue();

        ajustado.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        ajustado.Aprovacao.Justificativa.Should().BeNull();
        reprovado.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        reprovado.Aprovacao.Justificativa.Should().BeNull();
        aprovado.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
    }

    [Fact(DisplayName = "A edição recusada não devolve o tipo reprovado a pendente")]
    public void Tipo_EdicaoRecusada_MantemOReprovado()
    {
        TipoDocumento reprovado = TipoSolicitado();
        reprovado.ReprovarProtecao("Dado desnecessário.", Caroline, Terca);

        Atualizar(reprovado, nome: " ").IsFailure.Should().BeTrue();

        reprovado.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Reprovada);
    }

    [Fact(DisplayName = "Salvar o fato com ajuste recomendado ou reprovado, pelo descritivo, pela definição ou pelos valores, o devolve a pendente")]
    public void Fato_QualquerEdicaoDevolveAjusteEReprovadoAPendente()
    {
        static FatoCandidato Categorico()
        {
            FatoCandidato fato = FatoCandidato.CriarDoAdministrador(
                "ESCOLARIDADE", "Escolaridade", "Escolaridade do candidato.", DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global, null,
                "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
            fato.AdicionarValorDominio("MEDIO", "Ensino médio", 0, ativo: true);
            fato.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
            return fato;
        }

        FatoCandidato porDescritivo = Categorico();
        porDescritivo.ReprovarProtecao("Dado desnecessário.", Caroline, Terca);
        FatoCandidato porValor = Categorico();
        porValor.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Terca);
        FatoCandidato porDefinicao = Categorico();
        porDefinicao.ReprovarProtecao("Dado desnecessário.", Caroline, Terca);
        FatoCandidato porRegras = FatoCandidato.CriarDerivadoDoAdministrador(
            "TEM_RENDA_DERIVADA", "Tem renda derivada", "Se o candidato tem renda.", DominioFato.Booleano, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        porRegras.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        porRegras.ReprovarProtecao("Dado desnecessário.", Caroline, Terca);
        FatoCandidato aprovado = FatoAprovado(NovoFato());

        porDescritivo.AlterarDescritivo("Outro nome", "Escolaridade do candidato.", null).IsSuccess.Should().BeTrue();
        porValor.CorrigirValor("MEDIO", "Ensino médio completo", null).IsSuccess.Should().BeTrue();
        porDefinicao.AlterarDefinicao(Definicao(porDefinicao), Catalogo(porDefinicao)).IsSuccess.Should().BeTrue();
        porRegras.DefinirRegrasPadrao([], Catalogo(porRegras)).IsSuccess.Should().BeTrue();
        aprovado.AlterarDescritivo("Outro nome", "Outra descrição do fato.", null).IsSuccess.Should().BeTrue();

        porRegras.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        porDescritivo.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        porValor.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        porDefinicao.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
        aprovado.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
    }

    [Theory(DisplayName = "A reprovação sem justificativa, em branco ou com mais de 2.000 caracteres é recusada no campo")]
    [InlineData(null, "ProtecaoDoCatalogo.JustificativaObrigatoria")]
    [InlineData("   ", "ProtecaoDoCatalogo.JustificativaObrigatoria")]
    [InlineData("TAMANHO", "ProtecaoDoCatalogo.JustificativaMuitoLonga")]
    public void Tipo_ReprovarComJustificativaInvalida_Recusa(string? justificativa, string codigo)
    {
        TipoDocumento tipo = TipoSolicitado();
        string? enviada = justificativa == "TAMANHO" ? new string('a', 2001) : justificativa;

        Result resultado = tipo.ReprovarProtecao(enviada, Caroline, Terca);

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

    [Fact(DisplayName = "O fato do administrador nasce pendente")]
    public void Fato_NasceComoPendente() =>
        NovoFato().Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);

    [Fact(DisplayName = "Mudar a classificação do fato aprovado o devolve a pendente; mudar só o nome, a descrição ou o componente não")]
    public void Fato_SoAProtecaoReiniciaOCircuito()
    {
        FatoCandidato fato = FatoAprovado(NovoFato());

        fato.AlterarDescritivo("Outro nome", "Outra descrição do fato.", ComponenteDeExibicao.Moeda).IsSuccess.Should().BeTrue();
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);

        fato.AlterarDefinicao(Definicao(fato, classificacao: ClassificacaoDeProtecao.Sensivel, hipotese: HipoteseLegalTratamento.TutelaDaSaude), Catalogo(fato))
            .IsSuccess.Should().BeTrue();
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Pendente);
    }

    [Fact(DisplayName = "No fato de sistema o nome, a descrição e o componente se editam sem tirar a aprovação")]
    public void FatoDeSistema_MudarAApresentacao_MantemAprovado()
    {
        FatoCandidato fato = FatoAprovado(NovoFatoDeSistema());

        fato.AlterarDescritivo("Cor ou raça autodeclarada", "Conforme o IBGE.", ComponenteDeExibicao.Situacao).IsSuccess.Should().BeTrue();

        fato.ComponenteExibicao.Should().Be(ComponenteDeExibicao.Situacao);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
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
        fato.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Caroline, Terca).IsSuccess.Should().BeTrue();

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

    [Fact(DisplayName = "O ajuste não recomenda classificação abaixo do mínimo do domínio do fato")]
    public void Fato_RecomendarAjusteAbaixoDoMinimoDoDominio_Recusado()
    {
        FatoCandidato fato = NovoFato(DominioFato.Texto);
        fato.SolicitarAprovacaoDaProtecao(Isabella, Segunda);

        Result resultado = fato.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Publico, null, Caroline, Terca);

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
            "TEM_RENDA", "Tem renda", "Se o candidato tem renda.", dominio, CardinalidadeFato.Escalar, null, dominio == DominioFato.Texto ? FormatoTexto.Livre : null,
            "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

    private static FatoCandidato NovoFatoDeSistema() =>
        FatoCandidato.Criar(
            "COR_RACA", "Cor ou raça", "Cor ou raça que o candidato declara.", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
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
