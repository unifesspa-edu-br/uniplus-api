namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O vínculo dos itens, dos campos de grupo e dos termos ao formulário a que pertencem, e o que o
/// formulário declara de si (código, fase e obrigatoriedade) para as regras que o consultam pelo código.
/// </summary>
public sealed class VinculoAoFormularioTests
{
    [Fact(DisplayName = "Os itens que nascem com o formulário ficam ligados ao formulário criado")]
    public void DefinirFormulario_ComItensDaCriacao_LigaOsItensAoFormularioCriado()
    {
        ProcessoSeletivo processo = Processo();

        Result criado = processo.DefinirFormulario(
            PapelDoFormularioDeTeste.Inscricao, null, null, FormularioDeTeste.Paginas(), FormularioDeTeste.DadosBasicos());

        criado.IsSuccess.Should().BeTrue(criado.Error?.Message);
        Guid formularioId = processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.Id;
        processo.Campos.Should().NotBeEmpty().And.OnlyContain(f => f.FormularioId == formularioId);
    }

    [Fact(DisplayName = "Ao definir os itens, os itens e os campos dos grupos ficam ligados ao formulário")]
    public void DefinirFatosColetados_FormularioExistente_LigaItensEGruposAoFormulario()
    {
        ProcessoSeletivo processo = Processo();
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Habilitacao);

        Result definido = processo.DefinirItens([Item("QUILOMBOLA", 0)], PapelDoFormularioDeTeste.Habilitacao, [Grupo("COMPOSICAO_FAMILIAR", 1, Campo("MAIOR_IDADE"))]);

        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
        Guid formularioId = processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.Id;
        processo.Campos.Select(static f => f.FatoCodigo).Should().BeEquivalentTo(["QUILOMBOLA", "MAIOR_IDADE"]);
        processo.Campos.Should().OnlyContain(f => f.FormularioId == formularioId);
        processo.GruposColetados.Should().ContainSingle().Which.FormularioId.Should().Be(formularioId);
    }

    [Fact(DisplayName = "Ao definir os termos, os termos ficam ligados ao formulário")]
    public void DefinirTermosDoFormulario_LigaOsTermosAoFormulario()
    {
        ProcessoSeletivo processo = Processo();

        Result definido = processo.DefinirTermos([ApoioFormulariosDoProcesso.Termo("VERACIDADE")], PapelDoFormularioDeTeste.Habilitacao);

        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
        Guid formularioId = processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.Id;
        processo.TermosExigidos.Should().ContainSingle().Which.FormularioId.Should().Be(formularioId);
    }

    [Fact(DisplayName = "Com dois formulários, cada item e cada termo fica ligado só ao formulário que o define")]
    public void DefinirItensETermos_DoisFormularios_CadaUmLigadoAoSeu()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItens([Item("QUILOMBOLA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("CERTIFICADO", 0)], PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        processo.DefinirTermos([ApoioFormulariosDoProcesso.Termo("VERACIDADE")], PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        Guid inscricao = processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.Id;
        Guid habilitacao = processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.Id;
        processo.Campos.Single(static f => f.FatoCodigo == "QUILOMBOLA").FormularioId.Should().Be(inscricao);
        processo.Campos.Single(static f => f.FatoCodigo == "CERTIFICADO").FormularioId.Should().Be(habilitacao);
        processo.TermosExigidos.Should().ContainSingle().Which.FormularioId.Should().Be(habilitacao);
    }

    [Fact(DisplayName = "Aplicar o modelo sobre o formulário que já existe liga itens, grupos e termos ao mesmo formulário")]
    public void AplicarModeloDeFormulario_FormularioExistente_LigaAoFormularioQuePermanece()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItens([Item("QUILOMBOLA", 0)], PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        Guid formularioId = processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.Id;

        Result aplicado = processo.AplicarModeloDeFormulario(CopiaDoModelo(PapelDoFormularioDeTeste.Habilitacao));

        aplicado.IsSuccess.Should().BeTrue(aplicado.Error?.Message);
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.Id.Should().Be(formularioId);
        processo.Campos.Select(static f => f.FatoCodigo).Should().BeEquivalentTo(["COR_RACA", "PARENTESCO"]);
        processo.Campos.Should().OnlyContain(f => f.FormularioId == formularioId);
        processo.GruposColetados.Should().ContainSingle().Which.FormularioId.Should().Be(formularioId);
        processo.TermosExigidos.Should().ContainSingle().Which.FormularioId.Should().Be(formularioId);
    }

    [Fact(DisplayName = "A aplicação recusada deixa os itens do processo ligados ao formulário de antes")]
    public void AplicarModeloDeFormulario_Recusada_MantemOVinculoDosItens()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItensComFaseDeInscricao([Item("QUILOMBOLA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("DECLARACAO", 0, exibidoQuando: "QUILOMBOLA")], PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        Guid inscricao = processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.Id;

        Result aplicado = processo.AplicarModeloDeFormulario(CopiaDoModelo(PapelDoFormularioDeTeste.Inscricao));

        aplicado.IsFailure.Should().BeTrue();
        processo.Campos.Single(static f => f.FatoCodigo == "QUILOMBOLA").FormularioId.Should().Be(inscricao);
    }

    [Theory(DisplayName = "O formulário declara o próprio código, a fase e a obrigatoriedade")]
    [InlineData(PapelDoFormularioDeTeste.Inscricao, false)]
    [InlineData(PapelDoFormularioDeTeste.Habilitacao, true)]
    [InlineData(PapelDoFormularioDeTeste.IsencaoTaxa, true)]
    public void Declarado_EspelhaCodigoFaseEObrigatoriedade(PapelDoFormularioDeTeste papel, bool comFase)
    {
        Guid? fase = comFase ? Guid.CreateVersion7() : null;
        FormularioProcesso formulario = LigacaoAoFormulario.FormularioNoPapel(papel, fase);

        formulario.Declarado.Should().Be(new FormularioDeclarado(formulario.Codigo, fase, formulario.Obrigatorio));
    }

    [Fact(DisplayName = "A coleta declarada lista os formulários declarados, um por formulário do processo")]
    public void FormulariosDeclarados_ProcessoComDoisFormularios_ListaOsDois()
    {
        ProcessoSeletivo processo = Processo();
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Habilitacao);

        IReadOnlyList<FormularioDeclarado> declarados = processo.Declaracao().Coleta.FormulariosDeclarados;

        declarados.Should().BeEquivalentTo([.. processo.Formularios.Select(static f => f.Declarado)]);
        declarados.Should().HaveCount(2);
    }

    [Fact(DisplayName = "A coleta declarada sem formulário não declara nenhum")]
    public void FormulariosDeclarados_ProcessoSemFormulario_ListaVazia() =>
        Processo().Declaracao().Coleta.FormulariosDeclarados.Should().BeEmpty();

    [Fact(DisplayName = "O formulário é buscado pelo código, entre os formulários do processo")]
    public void FormularioDe_PeloCodigo_DevolveOFormularioDoCodigo()
    {
        ProcessoSeletivo processo = Processo();
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Habilitacao);
        FormularioProcesso habilitacao = processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!;

        processo.Declaracao().Coleta.FormularioDe(habilitacao.Codigo).Should().BeSameAs(habilitacao);
    }

    [Fact(DisplayName = "O código que nenhum formulário do processo tem não devolve formulário")]
    public void FormularioDe_CodigoDeOutroFormulario_DevolveNulo()
    {
        ProcessoSeletivo processo = Processo();
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        CodigoDoFormulario desconhecido = CodigoDoFormulario.Criar("FORMULARIO_FICTICIO").Value!;

        processo.Declaracao().Coleta.FormularioDe(desconhecido).Should().BeNull();
    }

    private static ProcessoSeletivo Processo() => ProcessoSeletivo.Criar(
        "PS Vínculo", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static FatoColetado Item(string fato, int ordem, string? exibidoQuando = null) => FatoColetadoDeTeste.Coletando(fato, ordem)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ExibidoQuando(exibidoQuando is null ? null : ExibicaoDeTeste.Quando(exibidoQuando, Operador.Igual, true))
        .NaSecao(FormularioDeTeste.Secao)
        .Construir();

    private static FatoColetado Campo(string fato) => FatoColetadoDeTeste.Coletando(fato, 0)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .Construir();

    private static GrupoColetado Grupo(string codigo, int ordem, params FatoColetado[] campos) =>
        GrupoColetadoDeTeste.Agrupando(codigo, ordem).ComCampos(campos).Construir();

    private static CopiaDeModeloDeFormulario CopiaDoModelo(PapelDoFormularioDeTeste papel) => new(
        FormularioDeTeste.CodigoDoPapel(papel),
        "Formulário",
        FormularioDeTeste.Paginas(papel),
        [Item("COR_RACA", 0)],
        [Grupo("COMPOSICAO_FAMILIAR", 1, Campo("PARENTESCO"))],
        [ApoioFormulariosDoProcesso.Termo("VERACIDADE")],
        [],
        new ModeloDeOrigem(Guid.CreateVersion7(), "MODELO_FICTICIO", "Modelo fictício"));
}
