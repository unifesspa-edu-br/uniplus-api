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
/// O circuito de aprovação da proteção de dados do fato (ADR-0153), do administrador e de sistema:
/// o que o deixa pendente, as transições e o que cada recusa protege.
/// </summary>
public sealed class AprovacaoDaProtecaoDoFatoTests
{
    [Fact(DisplayName = "O ajuste do fato recusa a recomendação igual à atual e a que a hipótese legal não admite")]
    public void Fato_RecomendarAjusteComRecomendacaoInvalida_Recusada()
    {
        FatoCandidato igual = NovoFato();
        igual.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        FatoCandidato incompativel = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato("TEM_CONTRATO", "Tem contrato", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.ExecucaoContrato)).Value!;
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
            Dominio = (TipoDoDado)(-1),
            Cardinalidade = (QuantidadeDeValores)(-1),
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

    [Fact(DisplayName = "O fato fora de solicitação não é reprovado")]
    public void Fato_ReprovarSemSolicitacao_Recusado()
    {
        NovoFato().ReprovarProtecao("Dado desnecessário.", Caroline, Terca).Error!.Code
            .Should().Be(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao);
    }

    [Fact(DisplayName = "Salvar o fato com ajuste recomendado ou reprovado, pelo descritivo, pela definição ou pelos valores, o devolve a pendente")]
    public void Fato_QualquerEdicaoDevolveAjusteEReprovadoAPendente()
    {
        static FatoCandidato Categorico()
        {
            FatoCandidato fato = CriacaoDoFato.CriarDoAdministrador(
                new DescritivoDoFato("ESCOLARIDADE", "Escolaridade", "Escolaridade do candidato."),
                new DefinicaoDoFato(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, FonteValoresFato.Catalogo, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
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
        FatoCandidato porRegras = CriacaoDoFato.CriarDerivadoDoAdministrador(
            new DescritivoDoFato("TEM_RENDA_DERIVADA", "Tem renda derivada", "Se o candidato tem renda."),
            new DefinicaoDoFato(TipoDoDado.SimOuNao, null, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
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
            Definicao(fato, dominio: TipoDoDado.SimOuNao, classificacao: ClassificacaoDeProtecao.Sensivel, hipotese: HipoteseLegalTratamento.TutelaDaSaude), Catalogo(fato));

        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);
        resultado.Error.Message.Should().Contain("domínio").And.Contain("travados");
        fato.Dominio.Should().Be(TipoDoDado.Opcao);
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
            Dominio = TipoDoDado.Opcao,
            SemeadoPeloSistema = true,
        });
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Aprovada);
        fato.Aprovacao.AceitaPor.Should().Be(Isabella);
    }

    [Fact(DisplayName = "O ajuste não recomenda classificação abaixo do mínimo do domínio do fato")]
    public void Fato_RecomendarAjusteAbaixoDoMinimoDoDominio_Recusado()
    {
        FatoCandidato fato = NovoFato(TipoDoDado.Texto);
        fato.SolicitarAprovacaoDaProtecao(Isabella, Segunda);

        Result resultado = fato.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Publico, null, Caroline, Terca);

        resultado.Errors.Should().ContainSingle(e => e.Field == "classificacaoRecomendada")
            .Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio);
        fato.Aprovacao.Situacao.Should().Be(SituacaoDaAprovacao.Solicitada);
    }

    private static FatoCandidato NovoFato(TipoDoDado dominio = TipoDoDado.SimOuNao) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato("TEM_RENDA", "Tem renda", "Se o candidato tem renda."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, null, dominio == TipoDoDado.Texto ? FormatoTexto.Livre : null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;

    private static FatoCandidato NovoFatoDeSistema() =>
        CriacaoDoFato.Criar(
            new DescritivoDoFato("COR_RACA", "Cor ou raça", "Cor ou raça que o candidato declara."),
            OrigemFato.Declarado,
            "CAMPO_FORMULARIO:COR_RACA",
            new DefinicaoDoFato(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, FonteValoresFato.Catalogo, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal),
            sistema: true).Value!;

    private static FatoCandidato FatoAprovado(FatoCandidato fato)
    {
        fato.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        fato.AprovarProtecao(Caroline, Terca);
        return fato;
    }

    private static DefinicaoDoFato Definicao(
        FatoCandidato fato,
        TipoDoDado? dominio = null,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        HipoteseLegalTratamento hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal) =>
        new(
            dominio ?? fato.Dominio, fato.Cardinalidade, fato.FonteValores, fato.Formato, fato.PontoResolucao, fato.Escopo,
            classificacao, Finalidade, hipotese);

    private static CatalogoDeFatos Catalogo(params FatoCandidato[] fatos) => new(fatos, []);
}
