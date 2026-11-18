namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// O fato de integração externa não tem a imunidade da publicação e por isso a regra de formulário que o cita é
/// recusada (UNI-REQ-0089), diferentemente do dado que o arquivo da importação traz, que vale como resposta
/// (UNI-REQ-0144). O gatilho de exigência continua aceitando várias fontes (UNI-REQ-0084).
/// </summary>
public sealed class FatoDeIntegracaoNasRegrasTests
{
    private const string Integracao = "situacao_no_cadastro_externo";

    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");

    private ConhecimentoDosFatos Conhecimento(params ColetaDoFormulario[] coletas) =>
        new(
            coletas,
            new CatalogoDosFatosCitados(new Dictionary<string, string> { [Integracao] = "HABILITACAO" }, [], new Dictionary<string, IReadOnlyCollection<string>>())
            {
                FatosDeIntegracao = [Integracao],
            },
            [_habilitacao]);

    [Fact(DisplayName = "A regra de campo que cita fato de integração externa é recusada pelo fato, mesmo conhecido na fase")]
    public void RegraDeCampo_CitaFatoDeIntegracao_Recusa()
    {
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        ColetaDoFormulario coleta = new(habilitacao, [new ItemDoGrafo("carteira", 1, null, [Integracao])], [], [], []);

        ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(coleta)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = $"formularios[{habilitacao.Codigo.Valor}].itens.carteira.fatosCitados.{Integracao}",
            Error = new { Code = GrafoFormularioErrorCodes.CitaFatoDeIntegracao },
        });
    }

    [Fact(DisplayName = "A exibição de seção que cita fato de integração externa também é recusada")]
    public void ExibicaoDeSecao_CitaFatoDeIntegracao_Recusa()
    {
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        ColetaDoFormulario coleta = new(habilitacao, [], [], [new EtapaDoGrafo("DADOS", 1, [Integracao])], []);

        ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(coleta)).Should().ContainSingle()
            .Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeIntegracao);
    }

    [Fact(DisplayName = "O gatilho de exigência que cita fato de integração externa é aceito")]
    public void GatilhoDeExigencia_CitaFatoDeIntegracao_Aceita()
    {
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        ConhecimentoDosFatos conhecimento = Conhecimento(ColetaDeTeste.Coleta(habilitacao, "carteira"));

        IReadOnlyList<FieldError> recusas = ConferenciaDosGatilhosDasExigencias.Conferir(
            conhecimento, [new GatilhoDaExigencia(habilitacao.Codigo, _habilitacao.Id, [Integracao])]);

        recusas.Should().BeEmpty();
    }
}
