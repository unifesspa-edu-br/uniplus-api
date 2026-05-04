namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public class DomainErrorMappingRegistryTests
{
    // ─── TryGetMapping — código existente ──────────────────────────────────

    [Fact]
    public void TryGetMapping_DadoCodigoExistente_DeveRetornarTrueEMapeamento()
    {
        DomainErrorMapping esperado = new(StatusCodes.Status404NotFound, "uniplus.selecao.edital.nao_encontrado", "Edital não encontrado");
        DomainErrorMappingRegistry registry = CriarRegistry(("Edital.NaoEncontrado", esperado));

        bool encontrado = registry.TryGetMapping("Edital.NaoEncontrado", out DomainErrorMapping? obtido);

        encontrado.Should().BeTrue();
        obtido.Should().Be(esperado);
    }

    [Fact]
    public void TryGetMapping_DadoCodigoInexistente_DeveRetornarFalse()
    {
        DomainErrorMappingRegistry registry = CriarRegistry();

        bool encontrado = registry.TryGetMapping("Codigo.Inexistente", out DomainErrorMapping? obtido);

        encontrado.Should().BeFalse();
        obtido.Should().BeNull();
    }

    // ─── Case-insensitive ──────────────────────────────────────────────────

    [Theory]
    [InlineData("edital.naoencontrado")]
    [InlineData("EDITAL.NAOENCONTRADO")]
    [InlineData("Edital.NaoEncontrado")]
    public void TryGetMapping_DeveSerCaseInsensitive(string variacaoDoCodigo)
    {
        DomainErrorMapping mapping = new(404, "uniplus.selecao.edital.nao_encontrado", "Edital não encontrado");
        DomainErrorMappingRegistry registry = CriarRegistry(("Edital.NaoEncontrado", mapping));

        bool encontrado = registry.TryGetMapping(variacaoDoCodigo, out DomainErrorMapping? obtido);

        encontrado.Should().BeTrue();
        obtido.Should().Be(mapping);
    }

    // ─── Múltiplas registrations ───────────────────────────────────────────

    [Fact]
    public void Construtor_ComMultiplasRegistrations_DeveMesclarTodasNoRegistry()
    {
        DomainErrorMapping mappingA = new(422, "uniplus.cpf.invalido", "CPF inválido");
        DomainErrorMapping mappingB = new(404, "uniplus.selecao.edital.nao_encontrado", "Edital não encontrado");

        DomainErrorMappingRegistry registry = CriarRegistry(
            ("Cpf.Invalido", mappingA),
            ("Edital.NaoEncontrado", mappingB));

        registry.TryGetMapping("Cpf.Invalido", out DomainErrorMapping? obtidoA).Should().BeTrue();
        registry.TryGetMapping("Edital.NaoEncontrado", out DomainErrorMapping? obtidoB).Should().BeTrue();
        obtidoA.Should().Be(mappingA);
        obtidoB.Should().Be(mappingB);
    }

    [Fact]
    public void Construtor_RegistrationPosteriorVenceConflitoDeMesmoCodigoOrdinalIgnoreCase()
    {
        DomainErrorMapping primeiro = new(400, "uniplus.primeiro", "Primeiro");
        DomainErrorMapping segundo = new(404, "uniplus.segundo", "Segundo");

        DomainErrorMappingRegistrationStub reg1 = new(("Codigo.X", primeiro));
        DomainErrorMappingRegistrationStub reg2 = new(("Codigo.X", segundo));
        DomainErrorMappingRegistry registry = new([reg1, reg2]);

        registry.TryGetMapping("Codigo.X", out DomainErrorMapping? obtido);

        obtido.Should().Be(segundo);
    }

    // ─── Guarda de nulls ───────────────────────────────────────────────────

    [Fact]
    public void Construtor_ComRegistrationsNulo_DeveLancarArgumentNullException()
    {
        Action acao = () => _ = new DomainErrorMappingRegistry(null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static DomainErrorMappingRegistry CriarRegistry(params (string Code, DomainErrorMapping Mapping)[] entradas)
    {
        DomainErrorMappingRegistrationStub registration = new(entradas);
        return new DomainErrorMappingRegistry([registration]);
    }

    private sealed class DomainErrorMappingRegistrationStub : IDomainErrorRegistration
    {
        private readonly IEnumerable<KeyValuePair<string, DomainErrorMapping>> _mappings;

        public DomainErrorMappingRegistrationStub(params (string Code, DomainErrorMapping Mapping)[] entradas)
        {
            _mappings = entradas.Select(e => new KeyValuePair<string, DomainErrorMapping>(e.Code, e.Mapping));
        }

        public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() => _mappings;
    }
}
