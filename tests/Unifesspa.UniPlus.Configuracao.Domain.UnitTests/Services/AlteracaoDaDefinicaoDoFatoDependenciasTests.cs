namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services.FatoDeTesteDaAlteracao;

/// <summary>
/// A alteração dos eixos do fato do administrador frente às dependências do próprio fato: classificação,
/// ponto de resolução, motivos de agregação e regras citadas (ADR-0136).
/// </summary>
public sealed class AlteracaoDaDefinicaoDoFatoDependenciasTests
{
    [Fact(DisplayName = "Classificação mais fraca que a de uma dependência é recusada na classificação, no derivado e no agregado")]
    public void ClassificacaoAbaixoDaDependencia_Recusa()
    {
        FatoCandidato renda = Declarado("TEM_RENDA", classificacao: ClassificacaoDeProtecao.Pessoal);
        FatoCandidato derivado = Derivado("PERFIL", classificacao: ClassificacaoDeProtecao.Pessoal);
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], Catalogo(renda, derivado)).IsSuccess.Should().BeTrue();
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", classificacao: ClassificacaoDeProtecao.Sensivel);
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro, ClassificacaoDeProtecao.Sensivel);

        derivado.AlterarDefinicao(Definicao(TipoDoDado.SimOuNao, classificacao: ClassificacaoDeProtecao.Publico), Catalogo(renda, derivado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia));
        agregado.AlterarDefinicao(Definicao(dominio: null, escopo: null, classificacao: ClassificacaoDeProtecao.Pessoal), Catalogo(membro, agregado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia));
    }

    [Fact(DisplayName = "Ponto de resolução anterior ao de uma dependência é recusado no ponto, no derivado e no agregado")]
    public void PontoAnteriorADependencia_Recusa()
    {
        FatoCandidato renda = Declarado("TEM_RENDA", ponto: "HABILITACAO");
        FatoCandidato derivado = Derivado("PERFIL", ponto: "HABILITACAO");
        CatalogoDeFatos catalogoDoDerivado = new([renda, derivado], InscricaoAntesDeHabilitacao);
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], catalogoDoDerivado).IsSuccess.Should().BeTrue();
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", ponto: "HABILITACAO");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro, ponto: "HABILITACAO", precedencias: InscricaoAntesDeHabilitacao);

        derivado.AlterarDefinicao(Definicao(TipoDoDado.SimOuNao, ponto: "INSCRICAO"), catalogoDoDerivado)
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("pontoResolucao", FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia));
        agregado.AlterarDefinicao(
                Definicao(dominio: null, escopo: null, ponto: "INSCRICAO"), new CatalogoDeFatos([membro, agregado], InscricaoAntesDeHabilitacao))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("pontoResolucao", FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia));
    }

    [Fact(DisplayName = "O agregado deixa de valer quando o membro troca de domínio, de fonte ou de escopo")]
    public void MotivosPelosQuaisDeixaDeAgregar_MembroMudou()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);
        FatoCandidato comOutraFonte = DeMembro("CATEGORIA_RENDA", fonte: FonteValoresFato.OpcoesDoProcesso);
        FatoCandidato booleano = DeMembro("CATEGORIA_RENDA", TipoDoDado.SimOuNao);
        FatoCandidato foraDeGrupo = Declarado("CATEGORIA_RENDA", TipoDoDado.Opcao, FonteValoresFato.Catalogo);
        CatalogoDeFatos catalogo = Catalogo(membro, agregado);

        agregado.MotivosPelosQuaisDeixaDeAgregar(membro, catalogo).Should().BeEmpty();
        agregado.MotivosPelosQuaisDeixaDeAgregar(comOutraFonte, catalogo).Should().ContainSingle().Which.Error.Message.Should().Contain("fonte");
        agregado.MotivosPelosQuaisDeixaDeAgregar(booleano, catalogo).Should().ContainSingle().Which.Error.Message.Should().Contain("domínio");
        agregado.MotivosPelosQuaisDeixaDeAgregar(foraDeGrupo, catalogo).Should().ContainSingle().Which.Error.Message.Should().Contain("membro de grupo");
    }

    [Fact(DisplayName = "Derivado e agregado inferem os eixos da mesma regra do cadastro, na alteração e na conferência")]
    public void EixosInferidos_SeguemOCadastro()
    {
        FatoCandidato derivado = Derivado("PERFIL");
        derivado.AlterarDefinicao(Definicao(TipoDoDado.Opcao), Catalogo(derivado)).IsSuccess.Should().BeTrue();
        FatoCandidato cadastrado = Derivado("OUTRO", TipoDoDado.Opcao);

        derivado.Should().BeEquivalentTo(new { cadastrado.Cardinalidade, cadastrado.FonteValores });

        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);
        agregado.MotivosPelosQuaisDeixaDeAgregar(membro, Catalogo(membro, agregado)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Domínio fora do vocabulário é recusado como inválido, sem a recusa de proteção mínima sobre ele")]
    public void DominioForaDoVocabulario_RecusaComoInvalidoNoCampo()
    {
        FatoCandidato derivado = Derivado("PERFIL");
        FatoCandidato declarado = Declarado("TEM_RENDA");
        CodigoInformado<TipoDoDado> foraDoVocabulario = CodigoInformado<TipoDoDado>.Ler("INEXISTENTE");

        derivado.AlterarDefinicao(Definicao(classificacao: ClassificacaoDeProtecao.Publico) with { Dominio = foraDoVocabulario }, Catalogo(derivado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("dominio", FatoCandidatoErrorCodes.DominioInvalido));
        derivado.AlterarDefinicao(Definicao(TipoDoDado.Texto, classificacao: ClassificacaoDeProtecao.Publico), Catalogo(derivado))
            .Errors.Select(static e => e.Field).Should().Equal("dominio");
        declarado.AlterarDefinicao(Definicao(cardinalidade: QuantidadeDeValores.ValorUnico, classificacao: ClassificacaoDeProtecao.Publico) with { Dominio = foraDoVocabulario }, Catalogo(declarado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("dominio", FatoCandidatoErrorCodes.DominioInvalido));
    }

    [Fact(DisplayName = "Recusa do derivado que já existia antes da alteração não a trava")]
    public void RecusaPreexistenteDoDerivado_NaoBloqueia()
    {
        // A precedência passou a pôr a inscrição antes da habilitação depois que a regra foi aceita.
        FatoCandidato renda = Declarado("TEM_RENDA", ponto: "HABILITACAO");
        FatoCandidato derivado = Derivado("PERFIL", ponto: "INSCRICAO");
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], Catalogo(renda, derivado)).IsSuccess.Should().BeTrue();
        CatalogoDeFatos comPrecedencia = new([renda, derivado], InscricaoAntesDeHabilitacao);

        derivado.AlterarDefinicao(Definicao(TipoDoDado.SimOuNao, ponto: "INSCRICAO", finalidade: "Outra finalidade."), comPrecedencia)
            .IsSuccess.Should().BeTrue("a recusa de ponto já existia e a alteração não a causa");
    }

    [Fact(DisplayName = "Recusa do agregado que já existia antes da alteração não a trava; a que a alteração causa, sim")]
    public void RecusaPreexistenteDoAgregado_NaoBloqueia_RecusaNova_Bloqueia()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", ponto: "HABILITACAO");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro, ponto: "HABILITACAO", precedencias: InscricaoAntesDeHabilitacao);
        CatalogoDeFatos catalogoDoAgregado = new([membro, agregado], InscricaoAntesDeHabilitacao);
        agregado.AlterarDefinicao(Definicao(dominio: null, escopo: null, ponto: "HABILITACAO", finalidade: "Outra finalidade."), catalogoDoAgregado)
            .IsSuccess.Should().BeTrue();
        agregado.AlterarDefinicao(Definicao(dominio: null, escopo: null, ponto: "INSCRICAO"), catalogoDoAgregado)
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia);
    }

    [Fact(DisplayName = "Agregado deixa de valer com o membro desativado")]
    public void MotivosPelosQuaisDeixaDeAgregar_MembroInativo()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);
        CatalogoDeFatos catalogo = Catalogo(membro, agregado);
        FatoCandidato inativo = DeMembro("CATEGORIA_RENDA");
        inativo.Desativar().IsSuccess.Should().BeTrue();

        agregado.MotivosPelosQuaisDeixaDeAgregar(inativo, catalogo).Should().ContainSingle().Which.Error.Message.Should().Contain("desativado");
    }

    [Fact(DisplayName = "A recusa preexistente cuja mensagem muda não conta como nova")]
    public void DiferencaDeRecusas_ComparaPorCampoECodigo_NaoPelaMensagem()
    {
        FieldError antes = new("pontoResolucao", new DomainError("X.Ponto", "resolve em INSCRICAO"), "regras[0].A");
        FieldError mesmaComOutroTexto = new("pontoResolucao", new DomainError("X.Ponto", "resolve em HABILITACAO"), "regras[0].A");
        FieldError outroCodigo = new("pontoResolucao", new DomainError("X.Outro", "resolve em INSCRICAO"), "regras[0].A");
        FieldError outroFatoCitado = new("pontoResolucao", new DomainError("X.Ponto", "resolve em INSCRICAO"), "regras[0].B");
        FieldError outraRegra = new("pontoResolucao", new DomainError("X.Ponto", "resolve em INSCRICAO"), "regras[1].A");

        DiferencaDeRecusas.Novas([mesmaComOutroTexto, outroCodigo, outroFatoCitado, outraRegra], [antes])
            .Should().Equal(outroCodigo, outroFatoCitado, outraRegra);
    }

    [Fact(DisplayName = "O motivo do agregado tem identidade estável")]
    public void MotivoDoAgregado_TemCodigoECampoEstaveis()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);
        FatoCandidato inativo = DeMembro("CATEGORIA_RENDA");
        inativo.Desativar().IsSuccess.Should().BeTrue();
        FieldError motivo = agregado.MotivosPelosQuaisDeixaDeAgregar(inativo, Catalogo(membro, agregado)).Should().ContainSingle().Subject;
        motivo.Error.Code.Should().Be(FatoCandidatoErrorCodes.AlteracaoQuebraAgregadoDoFato);
        motivo.Field.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Domínio omitido no derivado é recusado como obrigatório")]
    public void DerivadoSemDominio_RecusaComoObrigatorio()
    {
        FatoCandidato derivado = Derivado("PERFIL");

        derivado.AlterarDefinicao(Definicao(dominio: null), Catalogo(derivado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("dominio", FatoCandidatoErrorCodes.DominioObrigatorio));
    }

    [Fact(DisplayName = "Regra padrão de outro derivado que já tinha recusa contra uma dependência recusa a alteração que causa a mesma contra outra")]
    public void RegraPadraoDeOutroFato_RecusaContraOutraDependenciaNaMesmaRegra()
    {
        FatoCandidato a = Declarado("FATO_A");
        FatoCandidato b = Declarado("FATO_B");
        FatoCandidato derivado = Derivado("PERFIL");
        derivado.DefinirRegrasPadrao([Booleana(("FATO_A", true), ("FATO_B", true))], Catalogo(a, b, derivado)).IsSuccess.Should().BeTrue();
        CatalogoDeFatos catalogo = Catalogo(a, b, derivado);
        a.AlterarDefinicao(DeDeclarado(TipoDoDado.SimOuNao, classificacao: ClassificacaoDeProtecao.Sensivel), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> antes = [.. ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo).Errors];

        b.AlterarDefinicao(DeDeclarado(TipoDoDado.SimOuNao, classificacao: ClassificacaoDeProtecao.Sensivel), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> depois = [.. ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo).Errors];

        DiferencaDeRecusas.Novas(depois, antes).Should().ContainSingle()
            .Which.Should().Match<FieldError>(static e =>
                e.Error.Code == FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia && e.Origem == "regras[0].FATO_B");
    }

    [Fact(DisplayName = "Regra padrão de outro derivado cuja recusa contra a mesma dependência só muda de texto não recusa a alteração")]
    public void RegraPadraoDeOutroFato_MesmaRecusaComOutroTexto_NaoRecusa()
    {
        FatoCandidato a = Declarado("FATO_A");
        FatoCandidato derivado = Derivado("PERFIL");
        derivado.DefinirRegrasPadrao([Booleana(("FATO_A", true))], Catalogo(a, derivado)).IsSuccess.Should().BeTrue();
        CatalogoDeFatos catalogo = CatalogoComInscricaoAntesDeHabilitacaoEDoResultadoFinal(a, derivado);
        a.AlterarDefinicao(DeDeclarado(TipoDoDado.SimOuNao, ponto: "HABILITACAO"), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> antes = [.. ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo).Errors];

        a.AlterarDefinicao(DeDeclarado(TipoDoDado.SimOuNao, ponto: "RESULTADO_FINAL"), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> depois = [.. ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo).Errors];

        antes.Should().ContainSingle();
        depois.Should().ContainSingle().Which.Error.Message.Should().NotBe(antes[0].Error.Message);
        DiferencaDeRecusas.Novas(depois, antes).Should().BeEmpty();
    }

    [Fact(DisplayName = "Regra de aplicabilidade que já tinha recusa contra um fato recusa a alteração que causa a mesma contra outro")]
    public void RegraDeAplicabilidade_RecusaContraOutroFatoCitado()
    {
        FatoCandidato a = Declarado("FATO_A", ponto: "HABILITACAO");
        FatoCandidato b = Declarado("FATO_B");
        CatalogoDeFatos catalogo = Catalogo(a, b);
        PredicadoDnf regra = Quando(("FATO_A", true), ("FATO_B", true));
        List<FieldError> antes = [.. ConferenciaDaRegraDeAplicabilidade.Conferir(regra, regra, catalogo)];

        b.AlterarDefinicao(DeDeclarado(TipoDoDado.SimOuNao, ponto: "HABILITACAO"), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> depois = [.. ConferenciaDaRegraDeAplicabilidade.Conferir(regra, regra, catalogo)];

        antes.Should().ContainSingle();
        DiferencaDeRecusas.Novas(depois, antes).Should().ContainSingle()
            .Which.Should().Match<FieldError>(static e => e.Origem == "FATO_B");
    }

    [Fact(DisplayName = "Regra de aplicabilidade cuja recusa contra o mesmo fato só muda de texto não recusa a alteração")]
    public void RegraDeAplicabilidade_MesmaRecusaComOutroTexto_NaoRecusa()
    {
        FatoCandidato a = Declarado("FATO_A", ponto: "HABILITACAO");
        CatalogoDeFatos catalogo = Catalogo(a);
        PredicadoDnf regra = Quando(("FATO_A", true));
        List<FieldError> antes = [.. ConferenciaDaRegraDeAplicabilidade.Conferir(regra, regra, catalogo)];

        a.AlterarDefinicao(DeDeclarado(TipoDoDado.SimOuNao, ponto: "RESULTADO_FINAL"), catalogo).IsSuccess.Should().BeTrue();
        List<FieldError> depois = [.. ConferenciaDaRegraDeAplicabilidade.Conferir(regra, regra, catalogo)];

        depois.Should().ContainSingle().Which.Error.Message.Should().NotBe(antes.Should().ContainSingle().Subject.Error.Message);
        DiferencaDeRecusas.Novas(depois, antes).Should().BeEmpty();
    }

    [Fact(DisplayName = "Regras do próprio derivado: a regra que passa a resolver antes de outra dependência recusa mesmo com recusa antiga em outra regra")]
    public void RegrasDoProprioDerivado_RecusaARegraNovaMesmoComOutraJaRecusada()
    {
        (FatoCandidato derivado, CatalogoDeFatos catalogo) = DerivadoComDuasRegras();
        // Em HABILITACAO o derivado só resolve antes de A (RESULTADO_FINAL); antecipado para INSCRICAO, também de B.

        Result resultado = derivado.AlterarDefinicao(Definicao(ponto: "INSCRICAO"), catalogo);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "pontoResolucao"
            && e.Error.Code == FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia
            && e.Origem == "regras[1].FATO_B");
    }

    [Fact(DisplayName = "Regras do próprio derivado: a recusa que só muda de texto contra a mesma dependência não recusa a alteração")]
    public void RegrasDoProprioDerivado_MesmaRecusaComOutroTexto_NaoRecusa()
    {
        (FatoCandidato derivado, CatalogoDeFatos catalogo) = DerivadoComDuasRegras(segundaRegra: false);

        derivado.AlterarDefinicao(Definicao(ponto: "INSCRICAO"), catalogo).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Classificação e hipótese legal fora do vocabulário são recusadas como fora do vocabulário, não como ausentes")]
    public void ProtecaoForaDoVocabulario_RecusaComoForaDoVocabulario()
    {
        FatoCandidato declarado = Declarado("TEM_RENDA");

        declarado.AlterarDefinicao(
                DeDeclarado(TipoDoDado.SimOuNao) with
                {
                    ClassificacaoProtecao = CodigoInformado<ClassificacaoDeProtecao>.Ler("INEXISTENTE"),
                    HipoteseLegal = CodigoInformado<HipoteseLegalTratamento>.Ler("INEXISTENTE"),
                },
                Catalogo(declarado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [
                ("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoProtecaoInvalida),
                ("hipoteseLegal", FatoCandidatoErrorCodes.HipoteseLegalInvalida),
            ]);
    }

    private static CatalogoDeFatos CatalogoComInscricaoAntesDeHabilitacaoEDoResultadoFinal(params FatoCandidato[] fatos) =>
        new(
            fatos,
            [
                PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!,
                PrecedenciaFase.Criar("INSCRICAO", "RESULTADO_FINAL", permiteSobreposicao: false, []).Value!,
            ]);
}
