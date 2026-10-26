namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Leiautes;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Leiautes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O catálogo de leiautes: identidade por hash de definição, ativação e o arquivo de envio.
/// </summary>
public sealed class CatalogoDeLeiautesDoInepTests
{
    // Trava a definição publicada do retorno v1: mudar uma coluna, um leitor ou o dicionário de
    // situação exige uma versão nova, nunca a edição da v1.
    private const string HashDoRetornoV1 = "fd03f84ba37571853df57c1abb04c62f648e4f330720d9fc48b94038354612d0";

    [Fact(DisplayName = "o hash da definição publicada do retorno v1 não muda")]
    public void Hash_RetornoV1_EstaFixado()
    {
        CatalogoDeLeiautesDoInep.EnemRetornoV1.Hash.Should().Be(HashDoRetornoV1);
    }

    [Fact(DisplayName = "trocar o leitor de duas colunas muda o hash do leiaute")]
    public void Hash_TrocarLeitores_MudaOHash()
    {
        IReadOnlyList<ColunaDoRetorno> original = CatalogoDeLeiautesDoInep.EnemRetornoV1.Colunas;
        ColunaDoRetorno[] trocadas = [.. original];
        trocadas[3] = original[3] with { Leitor = original[8].Leitor };
        trocadas[8] = original[8] with { Leitor = original[3].Leitor };

        LeiauteDeRetornoDoInep alterado = LeiauteDeRetornoDoInep.Criar("INEP_ENEM_RETORNO", "v1", "alterado", true, trocadas);

        alterado.Hash.Should().NotBe(CatalogoDeLeiautesDoInep.EnemRetornoV1.Hash);
    }

    [Fact(DisplayName = "desativar um leiaute o tira da escolha mas ainda o devolve ao lote que o citou")]
    public void Ativos_LeiauteDesativado_SaiDaEscolhaMasContinuaObtenivel()
    {
        LeiauteDeRetornoDoInep desativado = CatalogoDeLeiautesDoInep.EnemRetornoV1.ComAtivo(false);
        CatalogoDeLeiautesDoInep catalogo = new([CatalogoDeLeiautesDoInep.EnemEnvioV1, desativado]);

        catalogo.Ativos(TipoDeLeiauteDoInep.Retorno).Should().BeEmpty();
        catalogo.Ativos(TipoDeLeiauteDoInep.Envio).Should().ContainSingle();
        catalogo.Obter("INEP_ENEM_RETORNO", "v1", TipoDeLeiauteDoInep.Retorno).Should().NotBeNull();
        catalogo.Obter("INEP_ENEM_RETORNO", "v1", TipoDeLeiauteDoInep.Envio).Should().BeNull();
    }

    [Fact(DisplayName = "desativar o leiaute não muda o hash, que é só da definição")]
    public void Hash_Desativar_NaoMudaOHash()
    {
        CatalogoDeLeiautesDoInep.EnemRetornoV1.ComAtivo(false).Hash.Should().Be(CatalogoDeLeiautesDoInep.EnemRetornoV1.Hash);
    }

    [Fact(DisplayName = "o envio escreve um CPF por linha, com quebra depois de cada uma, sem cabeçalho")]
    public void Escrever_UmCpfPorLinha_SemCabecalho()
    {
        string[] cpfs = [.. PersonasDeTeste.Candidatas.Take(3).Select(p => p.Cpf)];

        byte[] arquivo = EscritorDeArquivoDeEnvio.Escrever(CatalogoDeLeiautesDoInep.EnemEnvioV1, cpfs);

        Encoding.UTF8.GetString(arquivo).Should().Be($"{cpfs[0]}\n{cpfs[1]}\n{cpfs[2]}\n");
    }

    [Fact(DisplayName = "uma definição sem as cinco áreas é recusada na montagem do leiaute")]
    public void Criar_SemTodasAsAreas_Recusa()
    {
        ColunaDoRetorno[] colunas =
        [
            ColunaDoRetorno.NumeroDeInscricaoNoEnem(0),
            ColunaDoRetorno.Cpf(1),
            ColunaDoRetorno.Nota(2, AreaDoEnem.Matematica),
            ColunaDoRetorno.Situacao(3, AreaDoEnem.Matematica, new LeitorDeCodigoDeSituacaoDaProva(new Dictionary<string, SituacaoDaProvaNoEnem>())),
        ];

        Action montar = () => LeiauteDeRetornoDoInep.Criar("X", "v1", "incompleto", true, colunas);

        montar.Should().Throw<ArgumentException>();
    }
}
