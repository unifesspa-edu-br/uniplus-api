namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDoModeloDeFormulario;
using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>
/// A estrutura do modelo (seções, teto, duplicidades, restrições, declaração) e a ativação.
/// </summary>
public sealed class ModeloFormularioEstruturaECicloDeVidaTests
{
    [Fact(DisplayName = "O modelo nasce desativado, fora da escolha de processos novos, e é ativado quando pronto")]
    public void Criar_NasceDesativado_AtivacaoLiberaParaAEscolha()
    {
        ModeloFormulario modelo = Criar(DeOutraFaseObrigatoria, Conteudo([Item("CERTIFICADO", 0)])).Value!;

        modelo.Ativo.Should().BeFalse();
        modelo.Ativar().IsSuccess.Should().BeTrue();
        modelo.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Todo item do modelo está numa seção")]
    public void ItemSemSecao_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(DeOutraFaseObrigatoria, Conteudo([Item("CERTIFICADO_EMITIDO", 0, pagina: null)]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);
    }

    [Fact(DisplayName = "O item com impedimento fora do modelo de inscrição é recusado")]
    public void ImpedimentoNaHabilitacao_Recusa()
    {
        ItemDoModelo comImpedimento = Item("CERTIFICADO_EMITIDO", 0)
            .ComImpedimento(new Impedimento(Quando("CERTIFICADO_EMITIDO"), "Quem já tem o certificado não se habilita por este processo."));

        Result<ModeloFormulario> modelo = Criar(DeOutraFaseObrigatoria, Conteudo([comImpedimento]));

        modelo.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "conteudo.itens[0].regras.impedimento",
            Error = new { Code = ItemFormularioErrorCodes.ImpedimentoForaDaInscricao },
        });
    }

    [Fact(DisplayName = "O teto de itens do formulário vale para o modelo")]
    public void ItensAcimaDoTeto_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            DeOutraFaseObrigatoria, Conteudo([.. Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1).Select(static i => Item($"FATO_{i}", i))]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
    }

    [Fact(DisplayName = "Fatos que diferem por espaço são distintos")]
    public void FatoDiferentePorEspaco_NaoRepete()
    {
        Result<ModeloFormulario> modelo = Criar(DeOutraFaseObrigatoria, Conteudo([Item("CERTIFICADO", 0), Item("CERTIFICADO ", 1)]));

        modelo.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Página com espaço no código é recusada pela forma")]
    public void PaginaComEspaco_Recusa()
    {
        PaginaDoModelo outraDados = Dados with { Codigo = " DADOS", Ordem = 2 };

        Result<ModeloFormulario> modelo = Criar(
            DeOutraFaseObrigatoria, new ConteudoDoModelo("Habilitação", [Dados, outraDados, Revisao with { Ordem = 3 }], [Item("CERTIFICADO", 0)], [], [], []));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.PaginaCodigoInvalido);
    }

    [Fact(DisplayName = "A recusa de restrição aponta a posição em que ela veio")]
    public void RestricaoIncoerente_ApontaAPosicaoEnviada()
    {
        ItemDoModelo item = Item("IDADE", 0)
            .ComTipoDeCampo(TipoDeCampo.Numero)
            .ComRestricoes([new TamanhoTexto(1, 10), new FaixaNumerica(0, 10)]);

        Result<ModeloFormulario> modelo = Criar(DeOutraFaseObrigatoria, Conteudo([item]));

        FieldError recusa = modelo.Errors.Should().ContainSingle().Subject;
        recusa.Error.Code.Should().Be(ItemFormularioErrorCodes.RestricaoIncoerente);
        recusa.Field.Should().Be("conteudo.itens[0].regras.restricoes[0]");
    }

    [Fact(DisplayName = "Tipo de processo só com espaços é recusado, em vez de virar o modelo de todos os tipos")]
    public void TipoProcessoEmBranco_Recusa()
    {
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", new DescritivoDoModelo("Habilitação", null, "   "), DeOutraFaseObrigatoria, Conteudo([Item("CERTIFICADO", 0)]), SemDerivacoes);

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TipoProcessoCodigoEmBranco);
    }

    [Fact(DisplayName = "Declaração ausente é recusada no cadastro, não no conteúdo, e a estrutura que depende dela não é conferida")]
    public void DeclaracaoAusente_RecusaNoCadastro()
    {
        Result<ModeloFormulario> modelo = Criar(null, Conteudo([Item("CERTIFICADO", 0)]));

        modelo.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("declaracao", new DomainError(ModeloFormularioErrorCodes.DeclaracaoIncompleta, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message));
    }

    [Fact(DisplayName = "A edição recusada acumula o descritivo e o conteúdo e não muda o modelo; a conferência não muda nada")]
    public void Atualizar_Recusada_AcumulaERecusaSemMudarOModelo()
    {
        ModeloFormulario modelo = Criar(DeOutraFaseObrigatoria, Conteudo([Item("CERTIFICADO", 0)])).Value!;
        ConteudoDoModelo antes = modelo.Conteudo;
        DescritivoDoModelo invalido = new(" ", null, null);
        ConteudoDoModelo foraDeSecao = Conteudo([Item("CERTIFICADO", 0, pagina: null)]);

        List<FieldError> conferidas = modelo.ConferirAtualizacao(invalido, foraDeSecao, SemDerivacoes, conteudoPorInteiro: true);
        Result atualizada = modelo.Atualizar(invalido, foraDeSecao, SemDerivacoes);

        conferidas.Select(static e => e.Error.Code).Should().Equal(ModeloFormularioErrorCodes.NomeObrigatorio, EstruturaFormularioErrorCodes.ItemForaDeSecao);
        atualizada.Errors.Select(static e => e.Error.Code).Should().Equal(conferidas.Select(static e => e.Error.Code));
        modelo.Nome.Should().Be("Habilitação Medicina 2027");
        modelo.Conteudo.Should().BeSameAs(antes);
    }

    [Fact(DisplayName = "Desativar e reativar alternam o modelo; repetir o estado atual é recusado")]
    public void AtivarDesativar_AlternaERecusaORepetido()
    {
        ModeloFormulario modelo = Criar(DeOutraFaseObrigatoria, Conteudo([Item("CERTIFICADO", 0)])).Value!;
        modelo.Ativar().IsSuccess.Should().BeTrue();

        modelo.Ativar().Error!.Code.Should().Be(ModeloFormularioErrorCodes.JaAtivo);
        modelo.Desativar().IsSuccess.Should().BeTrue();
        modelo.Ativo.Should().BeFalse();
        modelo.Desativar().Error!.Code.Should().Be(ModeloFormularioErrorCodes.JaDesativado);
        modelo.Ativar().IsSuccess.Should().BeTrue();
        modelo.Ativo.Should().BeTrue();
    }
}
