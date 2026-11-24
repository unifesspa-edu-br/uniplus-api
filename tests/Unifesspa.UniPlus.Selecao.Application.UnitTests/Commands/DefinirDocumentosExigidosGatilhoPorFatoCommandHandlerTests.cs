namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
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
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoSexo()]);
        ColetarFato(cenario.Processo, "SEXO", TipoRenderizacao.EscolhaUnica);

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "SEXO", "IGUAL", "\"MASCULINO\"")));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = cenario.Processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.Condicoes.Should().ContainSingle(c => c.Fato == "SEXO");
    }

    /// <summary>
    /// Um gatilho que cita fato que o processo não resolve nunca se aplica a candidato nenhum:
    /// o fato fica indeterminado, e a exigência, pendente para sempre. Recusar é melhor que
    /// aceitar uma configuração que parece funcionar e não funciona. Aqui o fato existe no
    /// catálogo, mas o processo não o coleta.
    /// </summary>
    [Fact(DisplayName = "Gatilho que cita fato não resolvido pelo processo é recusado")]
    public async Task Handle_GatilhoFatoNaoResolvidoPeloProcesso_Recusa()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoSexo()]);

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "SEXO", "IGUAL", "\"MASCULINO\"")));

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
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoFaixaEtaria()]);
        if (coletaDataDeNascimento)
        {
            ColetarDataDeNascimento(cenario.Processo);
        }

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "FAIXA_ETARIA", "MAIOR_IGUAL", "18")));

        resultado.IsSuccess.Should().Be(coletaDataDeNascimento, resultado.Error?.Message);
    }

    [Fact(DisplayName = "Handle com gatilho de fato desconhecido retorna PredicadoDnf.FatoDesconhecido")]
    public async Task Handle_GatilhoFatoDesconhecido_RetornaErroNomeado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([]);

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "FATO_INEXISTENTE", "IGUAL", "\"X\"")));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    /// <summary>
    /// O processo oferta só a ampla concorrência. Sem modalidade ofertada, o processo não deriva a
    /// modalidade e a recusa seria outra: ofertar a ampla é o que deixa este teste chegar à
    /// conferência que ele de fato mede — o valor contra o domínio ofertado.
    /// </summary>
    [Fact(DisplayName = "CA-03: gatilho por MODALIDADE não ofertada pelo processo é recusado (integridade referencial)")]
    public async Task Handle_GatilhoModalidadeNaoOfertada_RetornaErroNomeado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoModalidade()]);
        OfertarAmplaConcorrencia(cenario.Processo);

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "MODALIDADE", "IGUAL", "\"LB_PPI\"")));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    /// <summary>O processo não oferece atendimento especializado: o atendimento especializado é nulo.</summary>
    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA aceita tipo do cadastro sem o processo oferecer atendimento especializado")]
    public async Task Handle_GatilhoTipoDeficienciaDoCadastroSemOferta_PersisteCondicao()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoTipoDeficiencia()]);
        ColetarFato(cenario.Processo, "TIPO_DEFICIENCIA", TipoRenderizacao.EscolhaUnica);

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"TEA\"")));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = cenario.Processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.Condicoes.Should().ContainSingle(c => c.Fato == "TIPO_DEFICIENCIA");
    }

    /// <summary>
    /// O valor de domínio do tipo de deficiência é o código do cadastro; o nome é só o rótulo,
    /// e renomeá-lo não invalida condição existente.
    /// </summary>
    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA usa o código do tipo, não o nome de exibição")]
    public async Task Handle_GatilhoTipoDeficiencia_UsaCodigoNaoNome()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoTipoDeficiencia()]);
        ColetarFato(cenario.Processo, "TIPO_DEFICIENCIA", TipoRenderizacao.EscolhaUnica);

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"DEFICIENCIA_VISUAL\"")));

        resultado.IsSuccess.Should().BeTrue("o código do tipo no cadastro é o valor de domínio");
    }

    /// <summary>A contraprova: se o domínio viesse do nome, este cenário passaria.</summary>
    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA com o nome de exibição é recusado")]
    public async Task Handle_GatilhoTipoDeficienciaPeloNome_Recusa()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoTipoDeficiencia()]);

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"Deficiência visual\"")));

        resultado.IsFailure.Should().BeTrue("o nome é rótulo de exibição, não valor de domínio");
    }

    /// <summary>O processo não oferece atendimento especializado: o atendimento especializado é nulo.</summary>
    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA fora do cadastro de tipos de deficiência é recusado (integridade referencial)")]
    public async Task Handle_GatilhoTipoDeficienciaForaDoCadastro_RetornaErroNomeado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoTipoDeficiencia()]);
        ColetarFato(cenario.Processo, "TIPO_DEFICIENCIA", TipoRenderizacao.EscolhaUnica);

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"NANISMO\"")));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    private static void ColetarDataDeNascimento(ProcessoSeletivo processo) =>
        processo.DefinirItens(
            [FatoColetadoDeTeste.Coletando("DATA_NASCIMENTO", 0)
                .ComRotulo("Data de nascimento")
                .DoTipo(TipoRenderizacao.Data)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir()])
            .IsSuccess.Should().BeTrue();

    private static FatoCandidatoView FatoSexo() => new(
        Guid.CreateVersion7(), "SEXO", "Sexo", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
        ["MASCULINO", "FEMININO", "INTERSEXO"], "INSCRICAO", "CAMPO_FORMULARIO:SEXO", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static FatoCandidatoView FatoModalidade() => new(
        Guid.CreateVersion7(), "MODALIDADE", "Modalidade de concorrência", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.VariosValores), null,
        "INSCRICAO", "REGRA_DERIVACAO:MODALIDADE", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Modalidade), Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    /// <summary>A faixa etária como o catálogo a publica: derivada, por atributo do candidato.</summary>
    private static FatoCandidatoView FatoFaixaEtaria() => new(
        Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Numerico), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
        null, "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    /// <summary>
    /// Oferta só a ampla concorrência. É o que torna um gatilho por modalidade resolvível: a
    /// modalidade de um candidato é derivada das modalidades ofertadas, não perguntada.
    /// </summary>
    private static void OfertarAmplaConcorrencia(ProcessoSeletivo processo) =>
        processo.DefinirDistribuicaoVagas([ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia()], FatosDeModalidadeDeTeste.DoCatalogo)
            .IsSuccess.Should().BeTrue();
}
