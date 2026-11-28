namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.CorpoDoPedido;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;

using Xunit.Abstractions;

public sealed class CorpoDoCadastroDeModeloDeFormularioTests(ITestOutputHelper saida)
{
    private static readonly IReadOnlyList<ConteudoDoModeloInput> ModelosSemeados = ConteudosDosModelosSemeados();

    [Fact]
    public void CadastroDeModelo_NoMaiorModeloSemeado_CabeNoLimiteDoCorpoComFolgaDeQuatroVezes()
    {
        CriarModeloFormularioCommand maiorSemeado = ModelosSemeados.Select(CadastroDoModeloSemeado).MaxBy(static c => MedidorDoCorpoDoPedido.Bytes(c))!;

        int bytesDoMaiorSemeado = MedidorDoCorpoDoPedido.Bytes(maiorSemeado);
        saida.WriteLine($"Cadastro de modelo, em bytes: maior semeado {bytesDoMaiorSemeado}; limite {LimiteDoCorpoDoPedido.Bytes}.");

        LimiteDoCorpoDoPedido.ComportaComFolga(bytesDoMaiorSemeado).Should().BeTrue();
    }

    /// <summary>
    /// O pior caso sintético (todos os itens do teto, com todo texto no tamanho máximo) não é pedido legítimo e passa do limite
    /// de propósito: o teste só registra a medida. O cadastro de modelo não tem teto em bytes próprio, e o teto de itens é o
    /// que o próximo teste guarda.
    /// </summary>
    [Fact]
    public void CadastroDeModelo_NoPiorCasoSintetico_RegistraAMedidaSemAfirmarSobreOLimite()
    {
        DimensoesDasSementesDoModelo dimensoes = DimensoesDasSementesDoModelo.De([.. ModelosSemeados]);
        CriarModeloFormularioCommand piorCaso = PiorCasoDoModeloDeFormulario.Cadastro(dimensoes, TipoDocumentoSeed.Itens.Count);

        int bytesDoPiorCaso = MedidorDoCorpoDoPedido.Bytes(piorCaso);
        saida.WriteLine($"Maiores quantidades das sementes: {dimensoes}. Pior caso sintético, em bytes: {bytesDoPiorCaso}.");

        bytesDoPiorCaso.Should().BePositive();
    }

    [Fact]
    public void CadastroDeModelo_NoPiorCasoValido_RespeitaOTetoDeItensDoFormulario()
    {
        DimensoesDasSementesDoModelo dimensoes = DimensoesDasSementesDoModelo.De([.. ModelosSemeados]);
        ConteudoDoModeloInput conteudo = PiorCasoDoModeloDeFormulario.Cadastro(dimensoes, 0).Conteudo!;

        int noTeto = FormaDoItem.QuantidadeNoTeto(conteudo.Itens!.Count, conteudo.Grupos!.Select(static g => g.Subitens!.Count));

        noTeto.Should().Be(FormaDoItem.MaximoDeItens);
    }

    private static CriarModeloFormularioCommand CadastroDoModeloSemeado(ConteudoDoModeloInput conteudo) => new(
        SementePsrMedicina2027.ModeloDeInscricaoComProva,
        new DeclaracaoDoModeloInput(true, true),
        new DescritivoDoModeloInput("Inscrição — PSR com prova", "Formulário de inscrição do Processo Seletivo Regular Unificado com prova.", "PSR"),
        conteudo);

    private static List<ConteudoDoModeloInput> ConteudosDosModelosSemeados()
    {
        Dictionary<string, string> formatos = FatoCandidatoSeed.Itens
            .Where(static f => f.Formato is not null)
            .ToDictionary(static f => f.Codigo, static f => f.Formato!.Value.Codigo(), StringComparer.Ordinal);

        return
        [
            ConjuntoBasicoDoModelo.Mesclar(ModelosDaSementePsrMedicina2027.Inscricao(comProva: true), formatos).ParaEntrada(),
            ConjuntoBasicoDoModelo.Mesclar(ModelosDaSementePsrMedicina2027.Inscricao(comProva: false), formatos).ParaEntrada(),
            ModelosDaSementePsrMedicina2027.Habilitacao().ParaEntrada(),
        ];
    }
}
