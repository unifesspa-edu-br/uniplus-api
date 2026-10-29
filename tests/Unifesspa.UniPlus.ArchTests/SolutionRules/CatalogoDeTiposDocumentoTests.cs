namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Guarda o seed do catálogo de tipos de documento contra o que o caminho de
/// escrita não guarda: as linhas são materializadas por <c>HasData</c>, sem passar
/// pela factory do agregado nem pelo handler, então nada impediria o banco de
/// nascer com dado que o cadastro rejeitaria.
/// </summary>
/// <remarks>
/// O recorte é deliberado — só o que produz falha concreta, não conferência de
/// estilo. Código fora do formato derruba toda leitura da tabela na reidratação;
/// código repetido trava a migração no índice único; categoria inexistente tira o
/// tipo de todo filtro por categoria.
/// </remarks>
public sealed class CatalogoDeTiposDocumentoTests
{
    [Fact(DisplayName = "Cada item do seed passa nas invariantes do agregado")]
    public void Seed_ItensRespeitamOAgregado()
    {
        // Coleta os recusados em vez de asserir item a item: a falha nomeia quais
        // códigos a factory rejeitou, em vez de dizer só que "algum" item falhou.
        string[] recusados = [.. TipoDocumentoSeed.Itens
            .Where(item => TipoDocumento.Criar(item.Codigo, item.Nome, null, item.Categoria, null, null, item.TipoEquivalente, item.ClassificacaoProtecao, TipoDocumentoSeed.FinalidadeTratamento, TipoDocumentoSeed.HipoteseLegal).IsFailure)
            .Select(item => item.Codigo)];

        recusados.Should().BeEmpty(
            "um item que a factory recusasse nasceria no banco como dado que o cadastro rejeita — "
            + "e o conversor derrubaria toda leitura da tabela ao reidratá-lo");
    }

    [Fact(DisplayName = "O seed não repete identificador nem código")]
    public void Seed_NaoRepeteIdentidade()
    {
        TipoDocumentoSeed.Itens.Select(i => i.Id).Should().OnlyHaveUniqueItems();
        TipoDocumentoSeed.Itens.Select(i => i.Codigo).Should().OnlyHaveUniqueItems(
            "o código é chave natural e o índice único parcial rejeitaria a duplicata na migração");
    }

    [Fact(DisplayName = "O seed traz os tipos que o edital de Medicina 2027 pede, cada um na categoria e na proteção que o documento exige")]
    public void Seed_TiposDoEditalDeMedicina_TemCategoriaEProtecao()
    {
        (string Codigo, string Categoria, ClassificacaoDeProtecao Protecao)[] esperados =
        [
            ("CIN", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
            ("CNH", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
            ("PASSAPORTE", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
            ("RNM", "IDENTIFICACAO", ClassificacaoDeProtecao.Identificador),
            ("BOLETIM_OCORRENCIA", "IDENTIFICACAO", ClassificacaoDeProtecao.Sensivel),
            ("DOCUMENTO_JUDICIAL_GUARDA", "IDENTIFICACAO", ClassificacaoDeProtecao.Sensivel),
            ("DECLARACAO_CONCLUSAO_ENSINO_MEDIO", "ESCOLARIDADE", ClassificacaoDeProtecao.Pessoal),
            ("CNIS", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("DECLARACAO_IRPJ", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("ECF", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("DECORE", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("DEFIS", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("DASN_SIMEI", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("DECLARACAO_INATIVIDADE_EMPRESA", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("DECLARACAO_RENDIMENTOS_AUFERIDOS", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("DECLARACAO_AUSENCIA_RENDIMENTOS_ALUGUEL", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("EXTRATO_PREVIDENCIA_PRIVADA", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("TRCT", "RENDA", ClassificacaoDeProtecao.Pessoal),
            ("LAUDO_PSICOLOGICO", "SAUDE", ClassificacaoDeProtecao.Sensivel),
            ("EXAME_AUDIOMETRIA", "SAUDE", ClassificacaoDeProtecao.Sensivel),
            ("EXAME_OFTALMOLOGICO", "SAUDE", ClassificacaoDeProtecao.Sensivel),
            ("TERMO_RESPONSABILIDADE_CERTIFICADO", "DOCUMENTO_PROCESSUAL", ClassificacaoDeProtecao.Pessoal),
        ];

        foreach ((string codigo, string categoria, ClassificacaoDeProtecao protecao) in esperados)
        {
            TipoDocumentoSeedItem item = TipoDocumentoSeed.Itens.Should().ContainSingle(i => i.Codigo == codigo).Which;
            item.Categoria.Should().Be(categoria, codigo);
            item.ClassificacaoProtecao.Should().Be(protecao, codigo);
        }
    }

    [Fact(DisplayName = "A CIN aponta o RG como equivalente, e o RG existe no catálogo")]
    public void Seed_CinEquivaleAoRg()
    {
        TipoDocumentoSeed.Itens.Single(i => i.Codigo == "CIN").TipoEquivalente.Should().Be("RG");
        TipoDocumentoSeed.Itens.Should().Contain(i => i.Codigo == "RG");
    }


    [Fact(DisplayName = "Toda categoria referenciada existe no cadastro semeado")]
    public void Seed_CategoriasExistemNoCadastro()
    {
        // A categoria é código de outro cadastro, sem chave estrangeira: nada no banco
        // impediria um tipo de nascer apontando para categoria que não existe, e o
        // handler só confere na escrita pelo CRUD — não no seed.
        HashSet<string> categoriasSemeadas = [.. CategoriaDocumentoSeed.Itens.Select(c => c.Codigo)];

        string[] orfaos = [.. TipoDocumentoSeed.Itens
            .Where(item => !categoriasSemeadas.Contains(item.Categoria))
            .Select(item => $"{item.Codigo} → {item.Categoria}")];

        orfaos.Should().BeEmpty("um tipo semeado em categoria inexistente sumiria de todo filtro por categoria");
    }
}
