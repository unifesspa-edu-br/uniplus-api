namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;

/// <summary>A ordem e a lista de campos ausentes no conteúdo do modelo são recusadas como obrigatórias, e nada é gravado com ordem zero.</summary>
public sealed class ModeloFormularioOrdemECamposAusentesTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    private static (string? Campo, string Codigo)[] Recusas(Result<Guid> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "O item sem ordem é recusado como obrigatório")]
    public async Task Criar_ItemSemOrdem_RecusaComoObrigatoria()
    {
        ConteudoDoModeloInput conteudo = Conteudo(Item("CERTIFICADO", 0) with { Ordem = null });

        Recusas(await _cenario.CriarAsync(conteudo)).Should().Equal(("conteudo.itens[0].ordem", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "A página sem ordem é recusada como obrigatória")]
    public async Task Criar_PaginaSemOrdem_RecusaComoObrigatoria()
    {
        ConteudoDoModeloInput conteudo = Conteudo(Item("CERTIFICADO", 0));
        conteudo = conteudo with { Paginas = [conteudo.Paginas![0] with { Ordem = null }, conteudo.Paginas[1]] };

        Recusas(await _cenario.CriarAsync(conteudo)).Should().Equal(("conteudo.paginas[0].ordem", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "O grupo sem ordem e sem campos sai com as duas recusas, cada uma no seu campo")]
    public async Task Criar_GrupoSemOrdemESemCampos_RecusaAsDuas()
    {
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO_FAMILIAR", 1).ComRotulo("Composição familiar").NaSecao("DADOS")
            .ComOcorrencias(1, 10).ComObrigatoriedade("SEMPRE").ComCampos(Campo("MAIOR_IDADE", 0)).Construir() with
        { Ordem = null, Subitens = null };

        Recusas(await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Grupos = [grupo] })).Should().Equal(
            ("conteudo.grupos[0].ordem", CodigosDeValidacao.Obrigatorio),
            ("conteudo.grupos[0].subitens", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "O grupo só sem a lista de campos é recusado como obrigatório, e não como lista vazia")]
    public async Task Criar_GrupoSoSemSubitens_RecusaSoOCampoObrigatorio()
    {
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO_FAMILIAR", 1).ComRotulo("Composição familiar").NaSecao("DADOS")
            .ComOcorrencias(1, 10).ComObrigatoriedade("SEMPRE").ComCampos(Campo("MAIOR_IDADE", 0)).Construir() with
        { Subitens = null };

        Recusas(await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Grupos = [grupo] })).Should().Equal(
            ("conteudo.grupos[0].subitens", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "O termo sem ordem é recusado como obrigatório")]
    public async Task Criar_TermoSemOrdem_RecusaComoObrigatoria()
    {
        TermoExigidoInput termo = new TermoExigidoInput(
            "TERMO_LGPD", 0, new VersaoDoTermoInput(Guid.CreateVersion7(), Guid.CreateVersion7()),
            new ExibicaoEObrigatoriedadeInput(null, new ObrigatoriedadeInput("SEMPRE"))) with
        { Ordem = null };

        Recusas(await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Termos = [termo] }))
            .Should().Contain(("conteudo.termos[0].ordem", CodigosDeValidacao.Obrigatorio));
    }
}
