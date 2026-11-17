namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

public sealed class PrevalidacaoDaInstituicaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 22, 17, 10, 0, TimeSpan.Zero);

    private static CriarInstituicaoCommand Comando(EnderecoGeoInput? endereco = null, string? cidadeCodigoIbge = null) => new(
        "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal",
        Cnpj: null, Mantenedora: null, CodigoMantenedoraEmec: null, Situacao: null, AtoCredenciamento: null,
        AtoRecredenciamento: null, ConceitoInstitucional: null, Igc: null, Website: null,
        endereco, cidadeCodigoIbge, cidadeCodigoIbge is null ? null : "Marabá", cidadeCodigoIbge is null ? null : "PA", null);

    private static EnderecoGeoInput EnderecoEmMaraba(string cep = "68507590") =>
        new(cep, "Folha 31", "s/n", null, "Nova Marabá", null,
            new CidadeReferenciaInput("1504208", "Marabá", "PA"),
            -5.3m, -49.1m, NivelResolucaoEndereco.Logradouro, "logradouro");

    [Fact(DisplayName = "Payload válido sem endereço é válido e não traz endereço")]
    public void Executar_PayloadValidoSemEndereco_ValidoSemEndereco()
    {
        (Result validacao, ReferenciaEnderecoGeo? endereco) = PrevalidacaoDaInstituicao.Executar(Comando(), Agora);

        validacao.IsSuccess.Should().BeTrue();
        endereco.Should().BeNull();
    }

    [Fact(DisplayName = "Payload válido com endereço devolve o endereço resolvido")]
    public void Executar_PayloadValidoComEndereco_DevolveOEnderecoResolvido()
    {
        (Result validacao, ReferenciaEnderecoGeo? endereco) =
            PrevalidacaoDaInstituicao.Executar(Comando(EnderecoEmMaraba(), "1504208"), Agora);

        validacao.IsSuccess.Should().BeTrue();
        endereco!.Cep.Should().Be("68507590");
    }

    [Fact(DisplayName = "Endereço de formato inválido entra na lista depois dos erros dos campos e não é devolvido")]
    public void Executar_EnderecoInvalido_AcrescentaOErroDoEnderecoAoFim()
    {
        CriarInstituicaoCommand comando = Comando(EnderecoEmMaraba("123"), "1504208") with { Sigla = "" };

        (Result validacao, ReferenciaEnderecoGeo? endereco) = PrevalidacaoDaInstituicao.Executar(comando, Agora);

        validacao.Errors.Select(erro => erro.Field).Should().Equal("sigla", "endereco");
        endereco.Should().BeNull();
    }
}
