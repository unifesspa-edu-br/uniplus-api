namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O número de opções de curso é dimensão do processo e depende das ofertas da distribuição de
/// vagas: nunca pode superar o número delas, e a declaração é obrigatória para publicar.
/// </summary>
public sealed class OpcoesDeCursoTests
{
    private const string MensagemAcimaDasOfertas = "Com 2 opções de curso, cadastre pelo menos dois cursos.";

    private static IEnumerable<string> Vermelhos(ProcessoSeletivo processo) => ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
        .Where(static i => !i.Ok)
        .Select(static i => i.Codigo);

    private static void DefinirOfertas(ProcessoSeletivo processo, int quantidade) =>
        processo.DefinirDistribuicaoVagas(
            [.. Enumerable.Range(0, quantidade).Select(static _ => ProcessoConformeFactory.Oferta())],
            FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

    private static ProcessoSeletivo ProcessoComOfertas(int quantidade, bool declararOpcoes = false)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararOpcoesDeCurso: false);
        DefinirOfertas(processo, quantidade);
        if (declararOpcoes)
        {
            processo.DefinirOpcoesDeCurso(2).IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    [Fact(DisplayName = "1 opção com 1 curso é aceita e gravada")]
    public void UmaOpcaoComUmCurso_Grava()
    {
        ProcessoSeletivo processo = ProcessoComOfertas(1);

        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();

        processo.NumeroDeOpcoesDeCurso.Should().Be(1);
    }

    [Fact(DisplayName = "2 opções com 1 curso são recusadas no campo numero, e nada é gravado")]
    public void DuasOpcoesComUmCurso_RecusaNoCampoNumero()
    {
        ProcessoSeletivo processo = ProcessoComOfertas(1);

        Result resultado = processo.DefinirOpcoesDeCurso(2);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("numero");
        resultado.Errors[0].Error.Code.Should().Be(ProcessoSeletivoErrorCodes.OpcoesDeCursoAcimaDasOfertas);
        resultado.Errors[0].Error.Message.Should().Be(MensagemAcimaDasOfertas);
        processo.NumeroDeOpcoesDeCurso.Should().BeNull("a recusa não declara nada");
    }

    [Theory(DisplayName = "Número fora de 1 e 2 é recusado no campo numero")]
    [InlineData(0)]
    [InlineData(3)]
    public void NumeroForaDeUmEDois_Recusa(int numero)
    {
        ProcessoSeletivo processo = ProcessoComOfertas(2);

        Result resultado = processo.DefinirOpcoesDeCurso(numero);

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("numero");
        resultado.Errors[0].Error.Code.Should().Be(ProcessoSeletivoErrorCodes.OpcoesDeCursoInvalido);
        processo.NumeroDeOpcoesDeCurso.Should().BeNull();
    }

    [Fact(DisplayName = "Reduzir a distribuição abaixo do número de opções é recusado, sem campo, e a distribuição fica como estava")]
    public void ReduzirADistribuicaoAbaixoDoNumero_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComOfertas(2, declararOpcoes: true);

        Result resultado = processo.DefinirDistribuicaoVagas(
            [ProcessoConformeFactory.Oferta()], FatosDeModalidadeDeTeste.DoCatalogo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().BeNull("o corpo da operação é só a lista de ofertas");
        resultado.Errors[0].Error.Code.Should().Be(ProcessoSeletivoErrorCodes.OpcoesDeCursoAcimaDasOfertas);
        processo.DistribuicaoVagas.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Distribuição vazia continua recusada pelo próprio motivo, antes da conferência das opções")]
    public void DistribuicaoVazia_RecusaPeloMotivoDela()
    {
        ProcessoSeletivo processo = ProcessoComOfertas(2, declararOpcoes: true);

        Result resultado = processo.DefinirDistribuicaoVagas([], FatosDeModalidadeDeTeste.DoCatalogo);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DistribuicaoVagasVazia");
    }

    [Fact(DisplayName = "2 opções sem distribuição gravada são aceitas, e o mínimo de cursos passa a ser cobrado quando há distribuição")]
    public void DuasOpcoesSemDistribuicao_AceitaEApontaAteHaverOfertas()
    {
        ProcessoSeletivo processo = ProcessoSemOfertas();

        processo.DefinirOpcoesDeCurso(2).IsSuccess.Should().BeTrue();

        Vermelhos(processo).Should().Contain("distribuicao_vagas_ausente").And.NotContain("opcoes_de_curso_acima_das_ofertas",
            "sem distribuição gravada a pendência é a da distribuição ausente");

        processo.DefinirDistribuicaoVagas([ProcessoConformeFactory.Oferta()], FatosDeModalidadeDeTeste.DoCatalogo)
            .Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.OpcoesDeCursoAcimaDasOfertas);

        DefinirOfertas(processo, 2);
        Vermelhos(processo).Should().NotContain("opcoes_de_curso_acima_das_ofertas");
    }

    private static ProcessoSeletivo ProcessoSemDistribuicao() => ProcessoSeletivo.Criar(
        "PS Opções", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    [Fact(DisplayName = "Publicar sem declarar o número de opções é recusado, e declarar libera")]
    public void SemDeclararONumero_RecusaPublicarEDeclararLibera()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararOpcoesDeCurso: false);

        Vermelhos(processo).Should().Equal("opcoes_de_curso_nao_declaradas");
        CausasDeConformidadeDeTeste.De(processo).Should().ContainSingle().Which.Error.Message.Should().Contain("Número de opções de curso");

        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();

        Vermelhos(processo).Should().BeEmpty();
        CausasDeConformidadeDeTeste.De(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Estado com mais opções que cursos não publica: a conferência recusa e o item acusa")]
    public void MaisOpcoesQueCursos_NaoPublica()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        ConfiguracaoDeclarada declaracao = DeclaracaoComMaisOpcoesQueOfertas(processo);

        ChecklistDeConformidade.AvaliarConformidade(declaracao, CadastrosDeTeste.Para(processo))
            .Where(static item => !item.Ok).Select(static item => item.Codigo)
            .Should().Equal("opcoes_de_curso_acima_das_ofertas");
        ChecklistDeConformidade.CausasDeConformidade(declaracao)
            .Should().ContainSingle().Which.Error.Message.Should().Contain("Opções de curso");
    }

    [Fact(DisplayName = "A regra vale na retificação: ela recusa o estado com mais opções que cursos")]
    public void Retificacao_ComMaisOpcoesQueCursos_Recusa()
    {
        ProcessoSeletivo processo = ProcessoPublicadoComDuasOpcoes(out VersaoConfiguracao versaoAbertura);
        ConfiguracaoDeclarada declaracao = DeclaracaoComMaisOpcoesQueOfertas(processo);

        TransicoesDaVersao.ConferirEstadoParaRetificar(processo.Id, processo.Situacao, versaoAbertura, "Reduzir cursos")
            .IsSuccess.Should().BeTrue();
        Result<ConfiguracaoAptaACongelar> conferencia = ConfiguracaoAptaACongelar.Conferir(
            processo.Id, declaracao, CadastrosDeTeste.Para(processo));

        conferencia.IsFailure.Should().BeTrue();
        conferencia.Errors.Should().Contain(erro => erro.Error.Message.Contains("Opções de curso"));
    }

    private static ProcessoSeletivo ProcessoPublicadoComDuasOpcoes(out VersaoConfiguracao versaoAbertura)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(declararOpcoesDeCurso: false);
        DefinirOfertas(processo, 2);
        processo.DefinirOpcoesDeCurso(2).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            new CenarioDeCongelamento { Dados = DadosEdital.Criar("001/2026", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30), Guid.CreateVersion7()).Value!, Documento = new DocumentoCongelado(Encoding.UTF8.GetBytes("{}"), "1.0", "canonical-json/sha256@v1", new string('a', 64)) });
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        versaoAbertura = publicacao.Value!;
        processo.ClearDomainEvents();
        return processo;
    }

    private static ConfiguracaoDeclarada DeclaracaoComMaisOpcoesQueOfertas(ProcessoSeletivo processo)
    {
        ConfiguracaoDeclarada original = processo.Declaracao();
        VagasDeclaradas vagas = original.Vagas;
        VagasDeclaradas inconsistentes = new(
            [vagas.DistribuicaoVagas[0]], 2, vagas.AplicaFada,
            vagas.Fada, vagas.Cascata, vagas.AtendimentoEspecializado);
        return new ConfiguracaoDeclarada(original.Identidade, original.Cronograma, original.Avaliacao,
            inconsistentes, original.Exigencias, original.Coleta, original.DivulgacaoETaxa)
        {
            ProvaPresencial = original.ProvaPresencial,
            MotivosDeDecisao = original.MotivosDeDecisao,
            Origem = original.Origem,
        };
    }

    [Fact(DisplayName = "1 opção sem distribuição gravada não acusa o mínimo de cursos")]
    public void UmaOpcaoSemDistribuicao_NaoAcusaAcimaDasOfertas()
    {
        ProcessoSeletivo processo = ProcessoSemDistribuicao();

        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();

        Vermelhos(processo).Should().NotContain("opcoes_de_curso_acima_das_ofertas");
    }

    private static ProcessoSeletivo ProcessoSemOfertas() => ProcessoSemDistribuicao();

}
