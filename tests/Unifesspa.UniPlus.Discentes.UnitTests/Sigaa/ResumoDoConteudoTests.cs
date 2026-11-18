namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

using static Unifesspa.UniPlus.Discentes.UnitTests.Sigaa.VinculosDaOrigem;

/// <summary>
/// O resumo do conteúdo do vínculo, que a sincronização compara para decidir se escreve.
/// </summary>
public sealed class ResumoDoConteudoTests
{
    public static readonly TheoryData<string, VinculoDiscentePayload> CamposQueMudamOConteudo = new()
    {
        { "matricula", Completo() with { Matricula = "201446010002" } },
        { "nome", Completo() with { Nome = "OUTRO NOME" } },
        { "nivel", Completo() with { Nivel = "M" } },
        { "identificador do curso", ComCurso(c => c with { Id = 999 }) },
        { "nome do curso", ComCurso(c => c with { Nome = "MATEMÁTICA" }) },
        { "código e-MEC", ComCurso(c => c with { CodigoEmec = "86318" }) },
        { "identificador do discente", Completo() with { IdDiscente = 24787 } },
        { "identificador da unidade", ComCurso(c => c with { Unidade = new UnidadePayload { Id = 7, Nome = "INSTITUTO DE CIENCIAS EXATAS" } }) },
        { "nome da unidade", ComCurso(c => c with { Unidade = new UnidadePayload { Id = 12, Nome = "OUTRA" } }) },
        { "identificador da situação", ComSituacao(s => s with { Id = 8 }) },
        { "descrição da situação", ComSituacao(s => s with { Descricao = "TRANCADO" }) },
        { "qualificador da situação", ComSituacao(s => s with { SituacaoVinculo = "TRC" }) },
        { "ano de ingresso", Completo() with { AnoIngresso = 2021 } },
        { "período de ingresso", Completo() with { PeriodoIngresso = 2 } },
    };

    /// <summary>
    /// O resumo vai para o banco: mudar a forma de calculá-lo faria a sincronização
    /// reescrever todos os vínculos de uma vez. O valor abaixo é o que a implementação
    /// produz hoje para o vínculo completo da origem.
    /// </summary>
    [Fact]
    public void Resumo_do_vinculo_completo_tem_o_valor_que_ja_esta_guardado()
    {
        string resumo = DecodificarUm(Completo()).ResumoDoConteudo.Valor;

        resumo.Should().Be("d85ee9e32e72d3827a3c238042f5deadcef88a28a2e3bed6c950ba1b7a19a2aa");
    }

    /// <summary>
    /// Dois vínculos diferentes: o primeiro tem a descrição da situação terminando onde o
    /// segundo tem o qualificador começando. Se a fronteira entre campos fosse marcada por
    /// um caractere que o próprio conteúdo pode conter, os dois produziriam o mesmo resumo
    /// — e uma alteração real na origem passaria por "nada mudou". Os valores são curtos
    /// porque o contrato limita o qualificador da situação a quatro caracteres; o que o
    /// teste exige deles é só que a emenda de um par caia no mesmo ponto que a do outro.
    /// </summary>
    [Fact]
    public void Conteudo_de_campo_nao_consegue_imitar_a_fronteira_entre_campos()
    {
        const char Caractere = '\u001f';

        VinculoDiscentePayload primeiro = ComSituacao(
            s => s with { Descricao = $"AT{Caractere}R", SituacaoVinculo = "X" });
        VinculoDiscentePayload segundo = ComSituacao(
            s => s with { Descricao = "AT", SituacaoVinculo = $"R{Caractere}X" });

        DecodificarUm(segundo).ResumoDoConteudo.Valor.Should().NotBe(
            DecodificarUm(primeiro).ResumoDoConteudo.Valor,
            "vínculos com conteúdos diferentes precisam ter resumos diferentes");
    }

    [Fact]
    public void Mesmo_conteudo_produz_o_mesmo_resumo()
    {
        string primeiro = DecodificarUm(Completo()).ResumoDoConteudo.Valor;
        string segundo = DecodificarUm(Completo()).ResumoDoConteudo.Valor;

        segundo.Should().Be(primeiro, "sem isso a sincronização reescreveria tudo todo dia");
    }

    [Theory]
    [MemberData(nameof(CamposQueMudamOConteudo))]
    public void Mudanca_em_qualquer_campo_guardado_altera_o_resumo(
        string nomeDoCampo,
        VinculoDiscentePayload alterado)
    {
        string original = DecodificarUm(Completo()).ResumoDoConteudo.Valor;
        string depois = DecodificarUm(alterado).ResumoDoConteudo.Valor;

        depois.Should().NotBe(
            original,
            "mudança em {0} precisa chegar à réplica, e é o resumo que decide se a escrita acontece",
            nomeDoCampo);
    }

    /// <summary>
    /// O resumo fica guardado ao lado dos demais campos, legíveis na mesma linha. Se
    /// cobrisse o CPF, quem tivesse a tabela conheceria todo o resto e poderia testar os
    /// pouco mais de um bilhão de CPFs válidos até reproduzir o resumo — devolvendo em
    /// claro o dado que a cifra protege. O preço está aqui declarado: correção isolada de
    /// CPF na origem não é percebida.
    /// </summary>
    [Fact]
    public void Resumo_nao_cobre_o_cpf_para_nao_desfazer_a_cifra_em_repouso()
    {
        string original = DecodificarUm(Completo()).ResumoDoConteudo.Valor;
        string comOutroCpf = DecodificarUm(Completo() with { Cpf = "11144477735" }).ResumoDoConteudo.Valor;

        comOutroCpf.Should().Be(
            original,
            "cobrir o CPF tornaria o resumo um caminho para recuperá-lo por tentativa e erro");
    }

    [Fact]
    public void Resumo_distingue_campo_ausente_de_campo_vazio()
    {
        string comAusente = DecodificarUm(ComCurso(c => c with { CodigoEmec = null })).ResumoDoConteudo.Valor;
        string comVazio = DecodificarUm(ComCurso(c => c with { CodigoEmec = "" })).ResumoDoConteudo.Valor;

        comVazio.Should().NotBe(comAusente, "ausência e vazio são estados diferentes na origem");
    }
}
