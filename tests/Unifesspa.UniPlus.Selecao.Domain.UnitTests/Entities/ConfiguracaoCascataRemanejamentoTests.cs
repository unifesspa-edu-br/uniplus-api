namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ConfiguracaoCascataRemanejamento.Criar"/> e
/// <see cref="DestinoRemanejamento.Criar"/> (RN-CASCATA-4, Story #575) — a
/// forma da cascata: ordens únicas/contíguas a partir de 1 por origem,
/// destinos sem repetição, origem nunca é destino de si mesma, limites de
/// tamanho e formato dos códigos. RN-CASCATA-5 (regra tipada e conteúdo
/// vinculado ao esquema_args) é responsabilidade do handler, coberta em
/// <c>DefinirCascataRemanejamentoCommandHandlerTests</c>.
/// </summary>
public sealed class ConfiguracaoCascataRemanejamentoTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static ReferenciaRegra RegraCascata() =>
        ReferenciaRegra.Criar(RegraRemanejamentoCodigo.Cascata, "v1", HashFixo).Value!;

    private static DestinoRemanejamento Destino(string origem, int ordem, string destino) =>
        DestinoRemanejamento.Criar(origem, ordem, destino).Value!;

    [Fact(DisplayName = "Criar com a matriz legal completa (8 origens × 7 destinos) tem sucesso")]
    public void Criar_MatrizLegalCompleta_Aceita()
    {
        string[] origens = ["LB_PPI", "LB_Q", "LB_PCD", "LB_EP", "LI_PPI", "LI_Q", "LI_PCD", "LI_EP"];
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            string[] ordemDestinos = [.. origens.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(Destino(origem, i + 1, ordemDestinos[i]));
            }
        }

        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), "AC", destinos);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Destinos.Should().HaveCount(56);
        resultado.Value!.FallbackCodigo.Should().Be("AC");
    }

    [Fact(DisplayName = "Criar com uma origem e um destino tem sucesso (caso mínimo)")]
    public void Criar_CasoMinimo_Aceita()
    {
        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), "AC", [Destino("LB_PPI", 1, "LB_Q")]);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com destino de ordem < 1 falha na factory de DestinoRemanejamento")]
    public void Criar_OrdemMenorQueUm_Recusa()
    {
        Result<DestinoRemanejamento> resultado = DestinoRemanejamento.Criar("LB_PPI", 0, "LB_Q");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.OrdemInvalida");
    }

    [Fact(DisplayName = "Criar com duas entradas na mesma (origem, ordem) falha")]
    public void Criar_OrdemDuplicadaNaMesmaOrigem_Recusa()
    {
        List<DestinoRemanejamento> destinos = [Destino("LB_PPI", 1, "LB_Q"), Destino("LB_PPI", 1, "LB_PCD")];

        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), "AC", destinos);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.OrdemDuplicadaNaOrigem");
    }

    [Fact(DisplayName = "Criar com buraco na sequência de ordens (1, 2, 4) falha")]
    public void Criar_OrdemNaoContigua_Recusa()
    {
        List<DestinoRemanejamento> destinos =
        [
            Destino("LB_PPI", 1, "LB_Q"),
            Destino("LB_PPI", 2, "LB_PCD"),
            Destino("LB_PPI", 4, "LB_EP"),
        ];

        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), "AC", destinos);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.OrdemNaoContigua");
    }

    [Fact(DisplayName = "Criar com destino repetido na mesma origem falha")]
    public void Criar_DestinoRepetidoNaOrigem_Recusa()
    {
        List<DestinoRemanejamento> destinos =
        [
            Destino("LB_PPI", 1, "LB_Q"),
            Destino("LB_PPI", 2, "LB_Q"),
        ];

        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), "AC", destinos);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.DestinoDuplicado");
    }

    [Fact(DisplayName = "Criar com origem igual ao destino falha na factory de DestinoRemanejamento")]
    public void Criar_OrigemIgualAoDestino_Recusa()
    {
        Result<DestinoRemanejamento> resultado = DestinoRemanejamento.Criar("LB_PPI", 1, "LB_PPI");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.OrigemIgualAoDestino");
    }

    [Theory(DisplayName = "Criar sem fallback (nulo, vazio ou fora do formato) falha")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ac")]
    [InlineData("AC-1")]
    public void Criar_FallbackInvalido_Recusa(string? fallback)
    {
        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(
            RegraCascata(), fallback!, [Destino("LB_PPI", 1, "LB_Q")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.FallbackObrigatorio");
    }

    [Fact(DisplayName = "Criar com lista de destinos vazia falha (SemDestinos)")]
    public void Criar_SemDestinos_Recusa()
    {
        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), "AC", []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.SemDestinos");
    }

    [Fact(DisplayName = "Criar com mais de 8 origens falha (ExcedeLimiteDeOrigens)")]
    public void Criar_ExcedeLimiteDeOrigens_Recusa()
    {
        List<DestinoRemanejamento> destinos = [];
        for (int i = 0; i < 9; i++)
        {
            destinos.Add(Destino($"ORIGEM_{i}", 1, "AC_DESTINO"));
        }

        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), "AC", destinos);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.ExcedeLimiteDeOrigens");
    }

    [Fact(DisplayName = "Criar com mais de 56 destinos falha (ExcedeLimiteDeDestinos)")]
    public void Criar_ExcedeLimiteDeDestinos_Recusa()
    {
        // Uma única origem com 57 destinos ainda respeita o teto de 8 origens,
        // isolando a asserção ao limite de destinos.
        List<DestinoRemanejamento> destinos = [];
        for (int i = 1; i <= 57; i++)
        {
            destinos.Add(Destino("LB_PPI", i, $"DESTINO_{i}"));
        }

        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), "AC", destinos);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.ExcedeLimiteDeDestinos");
    }

    [Theory(DisplayName = "Criar com código de origem ou destino fora de ^[A-Z0-9_]+$ ou acima de 60 caracteres falha")]
    [InlineData("lb_ppi", "LB_Q")]
    [InlineData("LB-PPI", "LB_Q")]
    [InlineData("LB_PPI", "lb_q")]
    [InlineData("LB_PPI", "LB-Q")]
    public void Criar_CodigoInvalido_Recusa(string origem, string destino)
    {
        Result<DestinoRemanejamento> resultado = DestinoRemanejamento.Criar(origem, 1, destino);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.CodigoInvalido");
    }

    [Fact(DisplayName = "Criar com código acima de 60 caracteres falha (CodigoInvalido)")]
    public void Criar_CodigoAcimaDoLimite_Recusa()
    {
        string codigoLongo = new('A', 61);

        Result<DestinoRemanejamento> resultado = DestinoRemanejamento.Criar(codigoLongo, 1, "LB_Q");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.CodigoInvalido");
    }

    [Fact(DisplayName = "ADR-0125: DestinoRemanejamento.Criar acumula as violações independentes de um mesmo item")]
    public void DestinoRemanejamento_Criar_CodigosInvalidosEOrdemInvalida_AcumulaAsTresViolacoes()
    {
        Result<DestinoRemanejamento> resultado = DestinoRemanejamento.Criar("lb-ppi", 0, "lb-q");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoCascataRemanejamento.CodigoInvalido",
            "ConfiguracaoCascataRemanejamento.CodigoInvalido",
            "ConfiguracaoCascataRemanejamento.OrdemInvalida",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: ConfiguracaoCascataRemanejamento.Criar acumula fallback inválido e excesso de destinos no mesmo lote")]
    public void Criar_FallbackInvalidoEExcedeLimiteDeDestinos_AcumulaAsDuasViolacoes()
    {
        List<DestinoRemanejamento> destinos = [];
        for (int i = 1; i <= 57; i++)
        {
            destinos.Add(Destino("LB_PPI", i, $"DESTINO_{i}"));
        }

        Result<ConfiguracaoCascataRemanejamento> resultado = ConfiguracaoCascataRemanejamento.Criar(RegraCascata(), "ac-invalido", destinos);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoCascataRemanejamento.FallbackObrigatorio",
            "ConfiguracaoCascataRemanejamento.ExcedeLimiteDeDestinos",
        ]);
    }
}
