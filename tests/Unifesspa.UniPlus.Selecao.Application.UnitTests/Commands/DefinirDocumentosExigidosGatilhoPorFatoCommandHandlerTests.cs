namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// O gatilho da exigência por fato do candidato: o fato escalar é resolvido pelo processo, a faixa etária depende da data de nascimento, a modalidade precisa estar ofertada e o tipo de deficiência usa o código do cadastro.
/// </summary>
public sealed class DefinirDocumentosExigidosGatilhoPorFatoCommandHandlerTests
{
    [Fact(DisplayName = "Handle com gatilho válido (fato escalar) resolve o vocabulário e persiste a condição")]
    public async Task Handle_GatilhoValidoEscalar_PersisteCondicao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoSexo()]));

        ColetarFato(processo, "SEXO", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "SEXO", "IGUAL", "\"MASCULINO\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.Condicoes.Should().ContainSingle(c => c.Fato == "SEXO");
    }

    /// <summary>
    /// Um gatilho que cita fato que o processo não resolve nunca se aplica a candidato nenhum:
    /// o fato fica indeterminado, e a exigência, pendente para sempre. Recusar é melhor que
    /// aceitar uma configuração que parece funcionar e não funciona.
    /// </summary>
    [Fact(DisplayName = "Gatilho que cita fato não resolvido pelo processo é recusado")]
    public async Task Handle_GatilhoFatoNaoResolvidoPeloProcesso_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoSexo()]));

        // O fato existe no catálogo, mas este processo não o coleta.
        CondicaoGatilhoInput condicao = new(0, "SEXO", "IGUAL", "\"MASCULINO\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoNaoColetadoPeloProcesso");
    }

    /// <summary>
    /// A faixa etária é calculada pelo sistema da data de nascimento (dependência declarada pelo
    /// mecanismo do derivado): o gatilho que a cita só é aceito quando o processo coleta a data de
    /// nascimento — sem ela, a faixa etária nunca se resolve.
    /// </summary>
    [Theory(DisplayName = "Gatilho por faixa etária é aceito só quando o processo coleta a data de nascimento")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_GatilhoPorFaixaEtaria_DependeDaDataDeNascimento(bool coletaDataDeNascimento)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        if (coletaDataDeNascimento)
        {
            processo.DefinirItens(
                [FatoColetado.Criar("DATA_NASCIMENTO", 0, "Data de nascimento", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!])
                .IsSuccess.Should().BeTrue();
        }

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoFaixaEtaria()]));

        CondicaoGatilhoInput condicao = new(0, "FAIXA_ETARIA", "MAIOR_IGUAL", "18");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().Be(coletaDataDeNascimento, resultado.Error?.Message);
    }

    [Fact(DisplayName = "Handle com gatilho de fato desconhecido retorna PredicadoDnf.FatoDesconhecido")]
    public async Task Handle_GatilhoFatoDesconhecido_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[]));

        CondicaoGatilhoInput condicao = new(0, "FATO_INEXISTENTE", "IGUAL", "\"X\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    [Fact(DisplayName = "CA-03: gatilho por MODALIDADE não ofertada pelo processo é recusado (integridade referencial)")]
    public async Task Handle_GatilhoModalidadeNaoOfertada_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // O processo oferta só a ampla concorrência.

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoModalidade()]));

        // Sem modalidade ofertada, o processo não deriva a modalidade e a recusa seria outra. Ofertar
        // a ampla é o que deixa este teste chegar à conferência que ele de fato mede — o valor contra
        // o domínio ofertado.
        OfertarAmplaConcorrencia(processo);
        CondicaoGatilhoInput condicao = new(0, "MODALIDADE", "IGUAL", "\"LB_PPI\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA aceita tipo do cadastro sem o processo oferecer atendimento especializado")]
    public async Task Handle_GatilhoTipoDeficienciaDoCadastroSemOferta_PersisteCondicao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Processo NÃO oferece atendimento especializado — AtendimentoEspecializado nulo.

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]));

        ColetarFato(processo, "TIPO_DEFICIENCIA", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"TEA\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.Condicoes.Should().ContainSingle(c => c.Fato == "TIPO_DEFICIENCIA");
    }

    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA usa o código do tipo, não o nome de exibição")]
    public async Task Handle_GatilhoTipoDeficiencia_UsaCodigoNaoNome()
    {
        // O valor de domínio do tipo de deficiência é o código do cadastro; o nome é só o rótulo,
        // e renomeá-lo não invalida condição existente.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]));

        ColetarFato(processo, "TIPO_DEFICIENCIA", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput porCodigo = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"DEFICIENCIA_VISUAL\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [porCodigo], [], null, Qualquer, null);

        Result<MutacaoAceita> resultado = await HandleAsync(
            mocks, new DefinirDocumentosExigidosCommand(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente));

        resultado.IsSuccess.Should().BeTrue(
            "o código do tipo no cadastro é o valor de domínio");
    }

    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA com o nome de exibição é recusado")]
    public async Task Handle_GatilhoTipoDeficienciaPeloNome_Recusa()
    {
        // A contraprova: se o domínio viesse do nome, este cenário passaria.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]));

        CondicaoGatilhoInput porNome = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"Deficiência visual\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [porNome], [], null, Qualquer, null);

        Result<MutacaoAceita> resultado = await HandleAsync(
            mocks, new DefinirDocumentosExigidosCommand(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente));

        resultado.IsFailure.Should().BeTrue("o nome é rótulo de exibição, não valor de domínio");
    }

    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA fora do cadastro de tipos de deficiência é recusado (integridade referencial)")]
    public async Task Handle_GatilhoTipoDeficienciaForaDoCadastro_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Processo NÃO oferece atendimento especializado — AtendimentoEspecializado nulo.

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]));

        ColetarFato(processo, "TIPO_DEFICIENCIA", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"NANISMO\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    private static FatoCandidatoView FatoSexo() => new(
        Guid.CreateVersion7(), "SEXO", "Sexo", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
        ["MASCULINO", "FEMININO", "INTERSEXO"], "INSCRICAO", "CAMPO_FORMULARIO:SEXO", null, "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static FatoCandidatoView FatoModalidade() => new(
        Guid.CreateVersion7(), "MODALIDADE", "Modalidade de concorrência", "Descrição do fato.", "CATEGORICO", "DERIVADO", "MULTIVALORADO", null,
        "INSCRICAO", "REGRA_DERIVACAO:MODALIDADE", null, "MODALIDADE", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    /// <summary>A faixa etária como o catálogo a publica: derivada, por atributo do candidato.</summary>
    private static FatoCandidatoView FatoFaixaEtaria() => new(
        Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", "Descrição do fato.", "NUMERICO", "DERIVADO", "ESCALAR",
        null, "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    /// <summary>
    /// Oferta só a ampla concorrência. É o que torna um gatilho por modalidade resolvível: a
    /// modalidade de um candidato é derivada das modalidades ofertadas, não perguntada.
    /// </summary>
    private static void OfertarAmplaConcorrencia(ProcessoSeletivo processo) =>
        processo.DefinirDistribuicaoVagas([ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
}
