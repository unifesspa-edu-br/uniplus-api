namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

using static Unifesspa.UniPlus.Discentes.UnitTests.Sigaa.VinculosDaOrigem;

/// <summary>
/// O que a decodificação recusa porque a origem entregou fora do contrato, ou algo que a
/// réplica não comporta.
/// </summary>
public sealed class DecodificadorDeVinculosContratoTests
{
    /// <summary>
    /// O contrato declara limite menor para estes campos, então passar do que a coluna
    /// comporta é, antes disso, passar do que a origem prometeu entregar.
    /// </summary>
    public static readonly TheoryData<string, VinculoDiscentePayload> TextosAlemDaReplica = new()
    {
        { "nome", Completo() with { Nome = Texto(LimitesDaReplica.Nome + 1) } },
        { "curso.nome", ComCurso(c => c with { Nome = Texto(LimitesDaReplica.NomeDoCurso + 1) }) },
        { "curso.codigoEmec", ComCurso(c => c with { CodigoEmec = Texto(LimitesDaReplica.CodigoEmecDoCurso + 1) }) },
        { "curso.unidade.nome", ComCurso(c => c with { Unidade = UnidadeCom(LimitesDaReplica.NomeDaUnidade + 1) }) },
        { "situacao.descricao", ComSituacao(s => s with { Descricao = Texto(LimitesDaReplica.DescricaoDaSituacao + 1) }) },
        { "situacao.situacaoVinculo", ComSituacao(s => s with { SituacaoVinculo = Texto(LimitesDaReplica.VinculoDaSituacao + 1) }) },
    };

    /// <summary>
    /// Tamanhos declarados no schema do contrato, escritos aqui por extenso de propósito: se
    /// o teste reusasse a constante da implementação, uma alteração dela passaria
    /// despercebida. As colunas são mais largas que o contrato em todos estes campos; um
    /// valor nessa faixa grava sem erro, e é justamente por isso que precisa ser recusado
    /// aqui.
    /// </summary>
    public static readonly TheoryData<string, VinculoDiscentePayload> TextosAlemDoContrato = new()
    {
        { "nome", Completo() with { Nome = Texto(201) } },
        { "curso.nome", ComCurso(c => c with { Nome = Texto(201) }) },
        { "curso.unidade.nome", ComCurso(c => c with { Unidade = UnidadeCom(201) }) },
        { "situacao.descricao", ComSituacao(s => s with { Descricao = Texto(21) }) },
        { "situacao.situacaoVinculo", ComSituacao(s => s with { SituacaoVinculo = Texto(5) }) },
    };

    /// <summary>
    /// Aqui a origem deixou de entregar o que prometeu, e seguir em frente corromperia a
    /// réplica.
    /// </summary>
    public static readonly TheoryData<string, VinculoDiscentePayload> CamposObrigatoriosAusentes = new()
    {
        { "matricula", Completo() with { Matricula = null } },
        { "nome", Completo() with { Nome = null } },
        { "nivel", Completo() with { Nivel = null } },
        { "cpf", Completo() with { Cpf = null } },
        { "curso", Completo() with { Curso = null } },
        { "situacao", Completo() with { Situacao = null } },
        { "idDiscente", Completo() with { IdDiscente = 0 } },
    };

    /// <summary>
    /// Mesmo num vínculo sem unidade acadêmica, a ausência de um campo obrigatório vale
    /// como entrega fora do combinado.
    /// </summary>
    public static readonly TheoryData<string, VinculoDiscentePayload> CamposAninhadosAusentes = new()
    {
        { "curso.id", SemUnidadeAcademica() with { Curso = SemUnidadeAcademica().Curso! with { Id = 0 } } },
        { "curso.nome", SemUnidadeAcademica() with { Curso = SemUnidadeAcademica().Curso! with { Nome = null } } },
        { "situacao.id", SemUnidadeAcademica() with { Situacao = Completo().Situacao! with { Id = 0 } } },
        { "situacao.descricao", SemUnidadeAcademica() with { Situacao = Completo().Situacao! with { Descricao = null } } },
    };

    /// <summary>
    /// A origem promete sequência de dígitos. Letra ou pontuação é entrega fora do combinado
    /// — e sem esta conferência o valor malformado entraria na réplica.
    /// </summary>
    [Theory]
    [InlineData("2014A6010001")]
    [InlineData("2014.6010001")]
    [InlineData("2014 6010001")]
    public void Matricula_com_caractere_que_nao_e_digito_conta_como_fora_do_contrato(string matricula)
    {
        ResultadoDaDecodificacao resultado = Decodificar(Completo() with { Matricula = matricula });

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be("matricula");
    }

    /// <summary>
    /// O contrato da origem aceita qualquer sequência de dígitos; a coluna, não. Se o
    /// vínculo chegasse ao banco, a gravação seria recusada e derrubaria o lote inteiro,
    /// levando junto os vínculos válidos que o acompanhassem.
    /// </summary>
    [Fact]
    public void Matricula_maior_que_a_replica_comporta_e_descartada_antes_da_gravacao()
    {
        VinculoDiscentePayload longa = Completo() with { Matricula = new string('9', LimitesDaReplica.Matricula + 1) };

        ResultadoDaDecodificacao resultado = Decodificar(longa, Completo());

        resultado.Aceitos.Should().ContainSingle("o vínculo válido da mesma página entra");
        resultado.Descartados.Should().ContainSingle()
            .Which.Motivo.Should().Be(MotivoDeDescarte.NaoCabeNaReplica);
        resultado.QuantidadeForaDoContrato.Should().Be(0, "a origem cumpriu o contrato; o limite é da réplica");
    }

    /// <summary>
    /// Mesma natureza do limite da matrícula: o contrato não impõe tamanho ao nível, a
    /// coluna sim, e o excesso derrubaria o lote inteiro na gravação.
    /// </summary>
    [Fact]
    public void Nivel_maior_que_a_replica_comporta_e_descartado_antes_da_gravacao()
    {
        VinculoDiscentePayload longo = Completo() with { Nivel = new string('G', LimitesDaReplica.Nivel + 1) };

        ResultadoDaDecodificacao resultado = Decodificar(longo, Completo());

        resultado.Aceitos.Should().ContainSingle();
        resultado.Descartados.Should().ContainSingle()
            .Which.Motivo.Should().Be(MotivoDeDescarte.NaoCabeNaReplica);
    }

    [Fact]
    public void Matricula_no_limite_e_aceita()
    {
        VinculoDiscentePayload noLimite = Completo() with { Matricula = new string('9', LimitesDaReplica.Matricula) };

        Decodificar(noLimite).Aceitos.Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(TextosAlemDaReplica))]
    public void Texto_alem_do_que_a_replica_comporta_e_descartado_antes_da_gravacao(
        string campo,
        VinculoDiscentePayload excedente)
    {
        ResultadoDaDecodificacao resultado = Decodificar(excedente, Completo());

        resultado.Aceitos.Should().ContainSingle("o vínculo válido da mesma página entra");
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be(campo);
        resultado.Descartados[0].Motivo.Should().Be(MotivoDeDescarte.ForaDoContrato);
    }

    [Theory]
    [MemberData(nameof(TextosAlemDoContrato))]
    public void Texto_que_cabe_na_coluna_mas_excede_o_contrato_conta_como_fora_do_contrato(
        string campo,
        VinculoDiscentePayload excedente)
    {
        ResultadoDaDecodificacao resultado = Decodificar(excedente, Completo());

        resultado.Aceitos.Should().ContainSingle("o vínculo válido da mesma página entra");
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be(campo);
        resultado.QuantidadeForaDoContrato.Should().Be(1, "a origem entregou além do que o contrato dela declara");
    }

    /// <summary>
    /// O contrato declara a situação como conjunto fechado. Os valores abaixo são positivos
    /// e ficam de fora dele de propósito — a origem não os trata como vínculo.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(13)]
    [InlineData(15)]
    public void Situacao_fora_do_vocabulario_do_contrato_e_descartada(int situacao)
    {
        VinculoDiscentePayload foraDoContrato = ComSituacao(s => s with { Id = situacao });

        ResultadoDaDecodificacao resultado = Decodificar(foraDoContrato, Completo());

        resultado.Aceitos.Should().ContainSingle("o vínculo válido da mesma página entra");
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be("situacao.id");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(14)]
    [InlineData(100)]
    public void Situacao_declarada_no_contrato_e_aceita(int situacao)
    {
        Decodificar(ComSituacao(s => s with { Id = situacao })).Aceitos.Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(CamposObrigatoriosAusentes))]
    public void Ausencia_de_campo_obrigatorio_descarta_como_fora_do_contrato(
        string campo,
        VinculoDiscentePayload quebrado)
    {
        ResultadoDaDecodificacao resultado = Decodificar(quebrado);

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be(campo);
    }

    [Theory]
    [MemberData(nameof(CamposAninhadosAusentes))]
    public void Campo_obrigatorio_aninhado_conta_como_fora_do_contrato_mesmo_com_registro_incompleto(
        string campo,
        VinculoDiscentePayload quebrado)
    {
        ResultadoDaDecodificacao resultado = Decodificar(quebrado);

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be(campo);
    }

    [Fact]
    public void Cpf_fora_do_formato_acordado_descarta_como_fora_do_contrato()
    {
        ResultadoDaDecodificacao resultado = Decodificar(Completo() with { Cpf = "00000000000" });

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be("cpf");
    }

    /// <summary>
    /// A conferência do formato do CPF precisa vir antes dos descartes. Se viesse depois,
    /// este vínculo — que também não tem unidade acadêmica, o caso mais frequente da origem
    /// — seria contado como "registro que não serve", e a quebra do contrato passaria
    /// despercebida justamente onde ela é mais provável de se esconder.
    /// </summary>
    [Fact]
    public void Cpf_malformado_conta_como_fora_do_contrato_e_nao_como_registro_incompleto()
    {
        VinculoDiscentePayload quebrado = SemUnidadeAcademica() with { Cpf = "00000000000" };

        ResultadoDaDecodificacao resultado = Decodificar(quebrado);

        resultado.QuantidadeForaDoContrato.Should().Be(
            1, "entrega fora do combinado não pode ser confundida com registro incompleto");
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be("cpf");
    }

    /// <summary>
    /// A conferência do CPF vem antes da dos textos: um vínculo com os dois defeitos é
    /// anotado pelo CPF.
    /// </summary>
    [Fact]
    public void Cpf_malformado_vem_antes_do_texto_longo_demais()
    {
        VinculoDiscentePayload quebrado = Completo() with { Cpf = "00000000000", Nome = Texto(LimitesDaReplica.Nome + 1) };

        ResultadoDaDecodificacao resultado = Decodificar(quebrado);

        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be("cpf");
    }

    /// <summary>
    /// Este é o risco que descartar em vez de falhar introduz: se a origem parar de entregar
    /// um campo obrigatório, nenhum vínculo entra e a execução termina sem escrever nada. A
    /// réplica não é corrompida — mas congela. A contagem separada de entregas fora do
    /// combinado é o que impede isso de passar por uma sincronização bem-sucedida com nada
    /// a fazer.
    /// </summary>
    [Fact]
    public void Contrato_rompido_para_a_pagina_inteira_fica_visivel_na_contagem()
    {
        VinculoDiscentePayload[] paginaInteiraSemCpf =
            [.. Enumerable.Range(1, 3).Select(id => Completo() with { IdDiscente = id, Cpf = null })];

        ResultadoDaDecodificacao resultado = Decodificar(paginaInteiraSemCpf);

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(
            3, "a contagem precisa distinguir isto de uma página só com registros incompletos");
        resultado.Descartados.Should().OnlyContain(d => d.Detalhe == "cpf");
    }

    private static UnidadePayload UnidadeCom(int tamanhoDoNome) => new() { Id = 12, Nome = Texto(tamanhoDoNome) };
}
