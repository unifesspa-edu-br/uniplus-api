namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ApoioDoFormularioApresentado;

/// <summary>A recusa de versão aposentada e de versão que não se prova, e a garantia de que a leitura pública não consulta o catálogo vivo.</summary>
public sealed class ObterFormularioApresentadoVersaoEReidratacaoTests
{
    /// <summary>
    /// Uma versão que já foi corrente e deixou de ser reconhecida quando o codec vivo avançou —
    /// usada SÓ no teste que prova a recusa (issue #1089); nenhum outro teste deste arquivo rotula
    /// o caso corrente com ela.
    /// </summary>
    private const string VersaoAposentada = "0.0.7";

    private const string EnvelopeFormaCorrente = """
            {
              "documentosExigidos": {"dataDeReferenciaDosDados": null}, "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": "Formulário de Inscrição", "modeloOrigem": null, "paginas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null},
                  {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}
                ], "termos": []}],
              "gruposDosFormularios": [], "dadosDosFormularios": []
            }
            """;

    private const string EnvelopeComReidratacaoRecusada = """
            {
              "documentosExigidos": {"dataDeReferenciaDosDados": null}, "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": null, "modeloOrigem": null, "paginas": [], "termos": []}],
              "gruposDosFormularios": [], "dadosDosFormularios": []
            }
            """;

    /// <summary>
    /// O teste central da issue #1089: a versão é aposentada, mas o JSON congelado tem a forma
    /// ATUAL e é válido — exatamente o cenário em que decidir pela forma (o que o handler fazia
    /// antes desta correção) mascara a recusa que o registro de codecs já dá em outras
    /// superfícies (ex.: a leitura da base da retificação). Um envelope malformado não
    /// provaria nada aqui: o handler já o recusava antes, por <c>CertamePublicado.EnvelopeInesperado</c>.
    /// O que só esta correção resolve é a versão desconhecida com bytes que "passariam" no shape.
    /// </summary>
    [Fact(DisplayName = "Versão aposentada com bytes de forma corrente recusa com EnvelopeCodec.VersaoDesconhecida, sem tentar decidir pela forma")]
    public async Task Handle_VersaoAposentadaComFormaCorrente_RetornaVersaoDesconhecida()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, EnvelopeFormaCorrente, VersaoAposentada);

        Result<FormularioApresentadoDoProcessoDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue(
            $"a versão '{VersaoAposentada}' deixou de ser reconhecida quando o codec vivo avançou para " +
            $"'{VersaoCorrenteReconhecida}' — bytes coincidentemente válidos na forma atual não a devolvem " +
            "à lista de capacidades reconhecidas");
        resultado.Error!.Code.Should().Be("EnvelopeCodec.VersaoDesconhecida");
    }

    [Fact(DisplayName = "Versão cujos bytes não se provam não serve formulário: a recusa da reidratação aflora")]
    public async Task Handle_ReidratacaoRecusada_AfloraARecusa()
    {
        Guid processoId = Guid.CreateVersion7();
        IRegistroCodecsEnvelope registro = RegistroComHashDivergente();

        ICertameDivulgadoRepository divulgacao = RepositorioComDivulgacao(processoId);
        Result<FormularioApresentadoDoProcessoDto> resultado = await ObterFormularioApresentadoQueryHandler.Handle(
            new ObterFormularioApresentadoQuery(processoId, FinalidadeFormulario.Inscricao),
            MockComVersaoVigente(processoId, EnvelopeComReidratacaoRecusada),
            divulgacao,
            registro,
            Enderecos(divulgacao),
            CancellationToken.None);

        resultado.Error!.Code.Should().Be("EnvelopeCodec.HashDivergente", "as regras saem da reidratação, e uma versão que não se prova não é servida");
    }

    private static IRegistroCodecsEnvelope RegistroComHashDivergente()
    {
        IRegistroCodecsEnvelope registro = CriarRegistroReconhecendo(VersaoCorrenteReconhecida);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(
            Result<EnvelopeReidratado>.Failure(new DomainError("EnvelopeCodec.HashDivergente", "Os bytes não batem com o hash.")));
        return registro;
    }

    /// <summary>
    /// Prova ESTRUTURAL (não comportamental) de que a leitura pública nunca reconsulta o
    /// catálogo: nenhum parâmetro do <c>Handle</c> é um leitor cross-módulo do catálogo de fatos
    /// (<c>IFatoCandidatoReader</c>) — os valores selecionáveis entregues vêm inteiramente do
    /// envelope congelado já persistido em <see cref="VersaoConfiguracao.ConfiguracaoCongelada"/>,
    /// nunca de uma releitura do cadastro vivo. É essa ausência estrutural, e não um mock que
    /// "não é chamado", que garante que alterar/inativar/remover descrições no catálogo vivo
    /// nunca muda a resposta deste endpoint.
    /// </summary>
    [Fact(DisplayName = "O grafo de dependências do Handle não alcança IFatoCandidatoReader — a leitura pública é imune ao catálogo vivo")]
    public void Handle_NaoDependeDeLeitorDeCatalogo()
    {
        System.Reflection.MethodInfo handle = typeof(ObterFormularioApresentadoQueryHandler)
            .GetMethod(nameof(ObterFormularioApresentadoQueryHandler.Handle))!;

        handle.GetParameters().Select(static p => p.ParameterType.Name).Should().NotContain(
            "IFatoCandidatoReader",
            "os valores selecionáveis do formulário público vêm do envelope congelado, nunca de uma releitura do " +
            "catálogo vivo — se o Handle injetasse o leitor cross-módulo, alterar/inativar/remover uma descrição " +
            "no cadastro mudaria a resposta de uma versão já publicada, o que a imutabilidade do envelope proíbe");
    }
}
