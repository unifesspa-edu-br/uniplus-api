namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;

/// <summary>
/// A edição do modelo devolve na mesma resposta as recusas da leitura, as do catálogo e as do domínio, e só muda
/// o modelo gravado quando não há nenhuma.
/// </summary>
public sealed class ModeloFormularioEdicaoAcumulaRecusasTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact(DisplayName = "Recusa de leitura, nome vazio e forma inválida saem juntos, sem mudar o modelo nem salvar")]
    public async Task Atualizar_RecusaDeLeituraDeDescritivoEDeForma_DevolveTodasSemMudarOModelo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        ConteudoDoModelo conteudoAntes = existente.Conteudo;
        ConteudoDoModeloInput conteudo = Conteudo(ItemNaoLido("CERTIFICADO", 0), ItemSemRotulo("DATA_NASCIMENTO", 1, "DATA"));

        Result resultado = await _cenario.AtualizarAsync(existente, new DescritivoDoModeloInput(string.Empty, null, null), conteudo);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("conteudo.itens[0].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida),
            ("descritivo.nome", ModeloFormularioErrorCodes.NomeObrigatorio),
            ("conteudo.itens[1].apresentacao.rotulo", ItemFormularioErrorCodes.RotuloObrigatorio));
        existente.Nome.Should().Be("Habilitação Medicina");
        existente.Conteudo.Should().BeSameAs(conteudoAntes);
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa só do catálogo deixa o modelo como estava")]
    public async Task Atualizar_RecusaSoDoCatalogo_NaoMudaOModelo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        ConteudoDoModelo conteudoAntes = existente.Conteudo;

        Result resultado = await _cenario.AtualizarAsync(existente, new DescritivoDoModeloInput("Outro nome", "Outra descrição", null), Conteudo(Item("FATO_FORA_DO_CATALOGO", 0)));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("conteudo.itens[0].fatoCodigo");
        existente.Nome.Should().Be("Habilitação Medicina");
        existente.Descricao.Should().BeNull();
        existente.Conteudo.Should().BeSameAs(conteudoAntes);
    }

    [Fact(DisplayName = "A recusa de domínio aponta a posição que veio quando um item anterior não se leu")]
    public async Task Atualizar_ItemNaoLidoAntes_RecusaDeDominioNaPosicaoDeEntrada()
    {
        ConteudoDoModeloInput conteudo = Conteudo(ItemNaoLido("CERTIFICADO", 0), ItemSemRotulo("DATA_NASCIMENTO", 1, "DATA"));

        Result resultado = await _cenario.AtualizarAsync(Existente(tipo: null), conteudo, tipo: null);

        resultado.Errors.Select(static e => e.Field).Should().Equal("conteudo.itens[0].regras.obrigatoriedade", "conteudo.itens[1].apresentacao.rotulo");
    }

    [Fact(DisplayName = "Página que a leitura recusa não gera recusa de estrutura para o item que aponta para ela")]
    public async Task Atualizar_PaginaNaoLida_RecusaSoALeitura()
    {
        ConteudoDoModeloInput conteudo = Conteudo(Item("CERTIFICADO", 0)) with
        {
            Paginas = [EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("DADOS", 0, "BLOCO_INEXISTENTE").ComTitulo("Dados").Construir()],
        };

        Result resultado = await _cenario.AtualizarAsync(Existente(tipo: null), conteudo, tipo: null);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("conteudo.paginas[0].bloco");
    }

    [Fact(DisplayName = "O conteúdo válido continua mudando o modelo e salvando")]
    public async Task Atualizar_ConteudoValido_MudaOModeloESalva()
    {
        ModeloFormulario existente = Existente(tipo: null);

        Result resultado = await _cenario.AtualizarAsync(existente, new DescritivoDoModeloInput("Nome novo", null, null), Conteudo(Item("DATA_NASCIMENTO", 0, "DATA")));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        existente.Nome.Should().Be("Nome novo");
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
