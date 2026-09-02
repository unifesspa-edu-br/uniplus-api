namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

public sealed class DecodificadorDeVinculosTests
{
    [Fact]
    public void Traduz_vinculo_completo_com_todos_os_campos()
    {
        ResultadoDaDecodificacao resultado = DecodificarPagina(Completo());

        resultado.Aceitos.Should().HaveCount(1);
        resultado.Descartados.Should().BeEmpty();

        VinculoDiscenteSnapshot snapshot = resultado.Aceitos[0].Vinculo.Snapshot;
        snapshot.IdDiscenteSigaa.Should().Be(24786);
        snapshot.Matricula.Should().Be("201446010001");
        snapshot.Cpf.Valor.Should().Be("52998224725");
        snapshot.Nivel.Should().Be("G");
        snapshot.Curso.Nome.Should().Be("CIÊNCIA DA COMPUTAÇÃO");
        snapshot.Curso.CodigoEmec.Should().Be("1269997");
        snapshot.Curso.UnidadeNome.Should().Be("INSTITUTO DE CIENCIAS EXATAS");
        snapshot.Ingresso.Ano.Should().Be(2020);
    }

    [Fact]
    public void Reflete_a_situacao_como_a_origem_a_entrega()
    {
        ResultadoDaDecodificacao resultado = DecodificarPagina(Completo());

        SituacaoAcademicaSnapshot situacao = resultado.Aceitos[0].Vinculo.Snapshot.Situacao;
        situacao.Id.Should().Be(1);
        situacao.Descricao.Should().Be("ATIVO", "a situação é espelhada, não traduzida");
        situacao.Vinculo.Should().Be("ATV");
    }

    [Fact]
    public void Descarta_vinculo_de_curso_sem_unidade_academica_e_segue()
    {
        // É o caso de mais de mil vínculos da origem. Descartar é decisão registrada;
        // interromper a leitura por causa deles pararia a sincronização todo dia.
        VinculoDiscentePayload semUnidade = Completo() with
        {
            Curso = Completo().Curso! with { Unidade = null },
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(semUnidade);

        resultado.Aceitos.Should().BeEmpty();
        resultado.Descartados.Should().ContainSingle()
            .Which.Motivo.Should().Be(MotivoDeDescarte.CursoSemUnidadeAcademica);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(2020, null)]
    [InlineData(null, null)]
    public void Descarta_vinculo_sem_periodo_de_ingresso_e_segue(int? ano, int? periodo)
    {
        VinculoDiscentePayload semIngresso = Completo() with
        {
            AnoIngresso = ano,
            PeriodoIngresso = periodo,
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(semIngresso);

        resultado.Aceitos.Should().BeEmpty();
        resultado.Descartados.Should().ContainSingle()
            .Which.Motivo.Should().Be(MotivoDeDescarte.SemPeriodoDeIngresso);
    }

    [Theory]
    [InlineData("2014A6010001")]
    [InlineData("2014.6010001")]
    [InlineData("2014 6010001")]
    public void Matricula_com_caractere_que_nao_e_digito_conta_como_fora_do_contrato(string matricula)
    {
        // A origem promete sequência de dígitos. Letra ou pontuação é entrega fora do
        // combinado — e sem esta conferência o valor malformado entraria na réplica.
        ResultadoDaDecodificacao resultado = DecodificarPagina(Completo() with { Matricula = matricula });

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be("matricula");
    }

    [Fact]
    public void Matricula_maior_que_a_replica_comporta_e_descartada_antes_da_gravacao()
    {
        // O contrato da origem aceita qualquer sequência de dígitos; a coluna, não. Se o
        // vínculo chegasse ao banco, a gravação seria recusada e derrubaria o lote inteiro,
        // levando junto os vínculos válidos que o acompanhassem.
        VinculoDiscentePayload longa = Completo() with
        {
            Matricula = new string('9', LimitesDaReplica.Matricula + 1),
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(longa, Completo());

        resultado.Aceitos.Should().ContainSingle("o vínculo válido da mesma página entra");
        resultado.Descartados.Should().ContainSingle()
            .Which.Motivo.Should().Be(MotivoDeDescarte.NaoCabeNaReplica);
        resultado.QuantidadeForaDoContrato.Should().Be(
            0, "a origem cumpriu o contrato; o limite é da réplica");
    }

    [Fact]
    public void Nivel_maior_que_a_replica_comporta_e_descartado_antes_da_gravacao()
    {
        // Mesma natureza do limite da matrícula: o contrato não impõe tamanho ao nível, a
        // coluna sim, e o excesso derrubaria o lote inteiro na gravação.
        VinculoDiscentePayload longo = Completo() with
        {
            Nivel = new string('G', LimitesDaReplica.Nivel + 1),
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(longo, Completo());

        resultado.Aceitos.Should().ContainSingle();
        resultado.Descartados.Should().ContainSingle()
            .Which.Motivo.Should().Be(MotivoDeDescarte.NaoCabeNaReplica);
    }

    [Theory]
    [InlineData("nome")]
    [InlineData("curso.nome")]
    [InlineData("curso.codigoEmec")]
    [InlineData("curso.unidade.nome")]
    [InlineData("situacao.descricao")]
    [InlineData("situacao.situacaoVinculo")]
    public void Texto_alem_do_que_a_replica_comporta_e_descartado_antes_da_gravacao(string campo)
    {
        // O contrato declara limite menor para estes campos, então passar do que a coluna
        // comporta é, antes disso, passar do que a origem prometeu entregar.
        VinculoDiscentePayload b = Completo();
        VinculoDiscentePayload excedente = campo switch
        {
            "nome" => b with { Nome = Texto(LimitesDaReplica.Nome + 1) },
            "curso.nome" => b with { Curso = b.Curso! with { Nome = Texto(LimitesDaReplica.NomeDoCurso + 1) } },
            "curso.codigoEmec" => b with { Curso = b.Curso! with { CodigoEmec = Texto(LimitesDaReplica.CodigoEmecDoCurso + 1) } },
            "curso.unidade.nome" => b with
            {
                Curso = b.Curso! with { Unidade = new UnidadePayload { Id = 12, Nome = Texto(LimitesDaReplica.NomeDaUnidade + 1) } },
            },
            "situacao.descricao" => b with { Situacao = b.Situacao! with { Descricao = Texto(LimitesDaReplica.DescricaoDaSituacao + 1) } },
            _ => b with { Situacao = b.Situacao! with { SituacaoVinculo = Texto(LimitesDaReplica.VinculoDaSituacao + 1) } },
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(excedente, Completo());

        resultado.Aceitos.Should().ContainSingle("o vínculo válido da mesma página entra");
        resultado.Descartados.Should().ContainSingle()
            .Which.Motivo.Should().Be(MotivoDeDescarte.ForaDoContrato);
    }

    // Tamanhos declarados no schema do contrato, escritos aqui por extenso de propósito: se
    // o teste reusasse a constante da implementação, uma alteração dela passaria despercebida.
    [Theory]
    [InlineData("nome", 201)]
    [InlineData("curso.nome", 201)]
    [InlineData("curso.unidade.nome", 201)]
    [InlineData("situacao.descricao", 21)]
    [InlineData("situacao.situacaoVinculo", 5)]
    public void Texto_que_cabe_na_coluna_mas_excede_o_contrato_conta_como_fora_do_contrato(
        string campo, int tamanho)
    {
        // As colunas são mais largas que o contrato em todos estes campos. Um valor nessa
        // faixa grava sem erro, e é justamente por isso que precisa ser recusado aqui: se
        // passasse, a origem entregaria além do que promete sem que nada apontasse.
        VinculoDiscentePayload b = Completo();
        VinculoDiscentePayload excedente = campo switch
        {
            "nome" => b with { Nome = Texto(tamanho) },
            "curso.nome" => b with { Curso = b.Curso! with { Nome = Texto(tamanho) } },
            "curso.unidade.nome" => b with
            {
                Curso = b.Curso! with { Unidade = new UnidadePayload { Id = 12, Nome = Texto(tamanho) } },
            },
            "situacao.descricao" => b with { Situacao = b.Situacao! with { Descricao = Texto(tamanho) } },
            _ => b with { Situacao = b.Situacao! with { SituacaoVinculo = Texto(tamanho) } },
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(excedente, Completo());

        resultado.Aceitos.Should().ContainSingle("o vínculo válido da mesma página entra");
        resultado.Descartados.Should().ContainSingle()
            .Which.Motivo.Should().Be(MotivoDeDescarte.ForaDoContrato);
        resultado.QuantidadeForaDoContrato.Should().Be(
            1, "a origem entregou além do que o contrato dela declara");
    }

    // O contrato declara a situação como conjunto fechado. Os valores abaixo são positivos e
    // ficam de fora dele de propósito — a origem não os trata como vínculo.
    [Theory]
    [InlineData(10)]
    [InlineData(13)]
    [InlineData(15)]
    public void Situacao_fora_do_vocabulario_do_contrato_e_descartada(int situacao)
    {
        VinculoDiscentePayload b = Completo();
        VinculoDiscentePayload forade = b with { Situacao = b.Situacao! with { Id = situacao } };

        ResultadoDaDecodificacao resultado = DecodificarPagina(forade, Completo());

        resultado.Aceitos.Should().ContainSingle("o vínculo válido da mesma página entra");
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle()
            .Which.Detalhe.Should().Be("situacao.id");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(14)]
    [InlineData(100)]
    public void Situacao_declarada_no_contrato_e_aceita(int situacao)
    {
        VinculoDiscentePayload b = Completo();

        DecodificarPagina(b with { Situacao = b.Situacao! with { Id = situacao } })
            .Aceitos.Should().ContainSingle();
    }

    private static string Texto(int tamanho) => new('A', tamanho);

    [Fact]
    public void Matricula_no_limite_e_aceita()
    {
        VinculoDiscentePayload noLimite = Completo() with
        {
            Matricula = new string('9', LimitesDaReplica.Matricula),
        };

        DecodificarPagina(noLimite).Aceitos.Should().ContainSingle();
    }

    [Fact]
    public void Descarte_nao_contamina_os_vizinhos_da_mesma_pagina()
    {
        VinculoDiscentePayload descartavel = Completo() with
        {
            IdDiscente = 99,
            Curso = Completo().Curso! with { Unidade = null },
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(
            Completo(), descartavel, Completo() with { IdDiscente = 77 });

        resultado.Aceitos.Should().HaveCount(2, "os vínculos completos continuam entrando");
        resultado.Descartados.Should().ContainSingle()
            .Which.IdDiscenteSigaa.Should().Be(99);
    }

    [Theory]
    [InlineData("matricula")]
    [InlineData("nome")]
    [InlineData("nivel")]
    [InlineData("cpf")]
    [InlineData("curso")]
    [InlineData("situacao")]
    [InlineData("idDiscente")]
    public void Ausencia_de_campo_obrigatorio_descarta_como_fora_do_contrato(string campo)
    {
        // O contraste com os descartes acima é o núcleo desta camada: aqui a origem
        // deixou de entregar o que prometeu, e seguir em frente corromperia a réplica.
        VinculoDiscentePayload quebrado = campo switch
        {
            "matricula" => Completo() with { Matricula = null },
            "nome" => Completo() with { Nome = null },
            "nivel" => Completo() with { Nivel = null },
            "cpf" => Completo() with { Cpf = null },
            "curso" => Completo() with { Curso = null },
            "situacao" => Completo() with { Situacao = null },
            _ => Completo() with { IdDiscente = 0 },
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(quebrado);

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle()
            .Which.Detalhe.Should().Be(campo);
    }

    [Fact]
    public void Cpf_fora_do_formato_acordado_descarta_como_fora_do_contrato()
    {
        ResultadoDaDecodificacao resultado = DecodificarPagina(Completo() with { Cpf = "00000000000" });

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be("cpf");
    }

    [Fact]
    public void Cpf_malformado_conta_como_fora_do_contrato_e_nao_como_registro_incompleto()
    {
        // A conferência do formato do CPF precisa vir antes dos descartes. Se viesse
        // depois, este vínculo — que também não tem unidade acadêmica, o caso mais
        // frequente da origem — seria contado como "registro que não serve", e a quebra do
        // contrato passaria despercebida justamente onde ela é mais provável de se esconder.
        VinculoDiscentePayload quebrado = Completo() with
        {
            Cpf = "00000000000",
            Curso = Completo().Curso! with { Unidade = null },
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(quebrado);

        resultado.QuantidadeForaDoContrato.Should().Be(
            1, "entrega fora do combinado não pode ser confundida com registro incompleto");
        resultado.Descartados.Should().ContainSingle().Which.Detalhe.Should().Be("cpf");
    }

    [Theory]
    [InlineData("curso.id")]
    [InlineData("curso.nome")]
    [InlineData("situacao.id")]
    [InlineData("situacao.descricao")]
    public void Campo_obrigatorio_aninhado_conta_como_fora_do_contrato_mesmo_com_registro_incompleto(string campo)
    {
        CursoPayload cursoSemUnidade = Completo().Curso! with { Unidade = null };

        VinculoDiscentePayload quebrado = campo switch
        {
            "curso.id" => Completo() with { Curso = cursoSemUnidade with { Id = 0 } },
            "curso.nome" => Completo() with { Curso = cursoSemUnidade with { Nome = null } },
            "situacao.id" => Completo() with
            {
                Curso = cursoSemUnidade,
                Situacao = Completo().Situacao! with { Id = 0 },
            },
            _ => Completo() with
            {
                Curso = cursoSemUnidade,
                Situacao = Completo().Situacao! with { Descricao = null },
            },
        };

        ResultadoDaDecodificacao resultado = DecodificarPagina(quebrado);

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(1);
        resultado.Descartados.Should().ContainSingle()
            .Which.Detalhe.Should().Be(campo);
    }

    [Fact]
    public void Contrato_rompido_para_a_pagina_inteira_fica_visivel_na_contagem()
    {
        // Este é o risco que descartar em vez de falhar introduz: se a origem parar de
        // entregar um campo obrigatório, nenhum vínculo entra e a execução termina sem
        // escrever nada. A réplica não é corrompida — mas congela. A contagem separada de
        // entregas fora do combinado é o que impede isso de passar por uma sincronização
        // bem-sucedida com nada a fazer.
        VinculoDiscentePayload[] paginaInteiraSemCpf =
        [
            Completo() with { IdDiscente = 1, Cpf = null },
            Completo() with { IdDiscente = 2, Cpf = null },
            Completo() with { IdDiscente = 3, Cpf = null },
        ];

        ResultadoDaDecodificacao resultado = DecodificarPagina(paginaInteiraSemCpf);

        resultado.Aceitos.Should().BeEmpty();
        resultado.QuantidadeForaDoContrato.Should().Be(
            3, "a contagem precisa distinguir isto de uma página só com registros incompletos");
        resultado.Descartados.Should().OnlyContain(d => d.Detalhe == "cpf");
    }

    [Fact]
    public void Registro_incompleto_nao_conta_como_entrega_fora_do_combinado()
    {
        // O contraste do teste acima: descarte por registro incompleto é rotina, tem
        // volume conhecido, e não deve acionar suspeita de contrato rompido.
        ResultadoDaDecodificacao resultado = DecodificarPagina(
            Completo() with { Curso = Completo().Curso! with { Unidade = null } },
            Completo() with { AnoIngresso = null });

        resultado.Descartados.Should().HaveCount(2);
        resultado.QuantidadeForaDoContrato.Should().Be(0);
    }

    [Fact]
    public void Conteudo_de_campo_nao_consegue_imitar_a_fronteira_entre_campos()
    {
        // Dois vínculos diferentes: o primeiro tem a descrição da situação terminando onde
        // o segundo tem o qualificador começando. Se a fronteira entre campos fosse marcada
        // por um caractere que o próprio conteúdo pode conter, os dois produziriam o mesmo
        // resumo — e uma alteração real na origem passaria por "nada mudou".
        // Os valores são curtos porque o contrato limita o qualificador da situação a
        // quatro caracteres; o que o teste exige deles é só que a emenda de um par caia no
        // mesmo ponto que a do outro.
        const char Caractere = '\u001f';

        VinculoDiscentePayload primeiro = Completo() with
        {
            Situacao = Completo().Situacao! with
            {
                Descricao = $"AT{Caractere}R",
                SituacaoVinculo = "X",
            },
        };

        VinculoDiscentePayload segundo = Completo() with
        {
            Situacao = Completo().Situacao! with
            {
                Descricao = "AT",
                SituacaoVinculo = $"R{Caractere}X",
            },
        };

        DecodificarUm(segundo).ResumoDoConteudo.Should().NotBe(
            DecodificarUm(primeiro).ResumoDoConteudo,
            "vínculos com conteúdos diferentes precisam ter resumos diferentes");
    }

    [Fact]
    public void Mesmo_conteudo_produz_o_mesmo_resumo()
    {
        string primeiro = DecodificarUm(Completo()).ResumoDoConteudo;
        string segundo = DecodificarUm(Completo()).ResumoDoConteudo;

        segundo.Should().Be(primeiro, "sem isso a sincronização reescreveria tudo todo dia");
    }

    [Theory]
    [MemberData(nameof(CamposQueMudamOConteudo))]
    public void Mudanca_em_qualquer_campo_guardado_altera_o_resumo(
        string nomeDoCampo,
        VinculoDiscentePayload alterado)
    {
        string original = DecodificarUm(Completo()).ResumoDoConteudo;
        string depois = DecodificarUm(alterado).ResumoDoConteudo;

        depois.Should().NotBe(
            original,
            "mudança em {0} precisa chegar à réplica, e é o resumo que decide se a escrita acontece",
            nomeDoCampo);
    }

    public static TheoryData<string, VinculoDiscentePayload> CamposQueMudamOConteudo()
    {
        VinculoDiscentePayload b = Completo();

        return new TheoryData<string, VinculoDiscentePayload>
        {
            { "matricula", b with { Matricula = "201446010002" } },
            { "nome", b with { Nome = "OUTRO NOME" } },
            { "nivel", b with { Nivel = "M" } },
            { "identificador do curso", b with { Curso = b.Curso! with { Id = 999 } } },
            { "nome do curso", b with { Curso = b.Curso! with { Nome = "MATEMÁTICA" } } },
            { "código e-MEC", b with { Curso = b.Curso! with { CodigoEmec = "86318" } } },
            { "unidade", b with { Curso = b.Curso! with { Unidade = new UnidadePayload { Id = 7, Nome = "OUTRA" } } } },
            { "situação", b with { Situacao = b.Situacao! with { Id = 8, Descricao = "TRANCADO" } } },
            { "qualificador da situação", b with { Situacao = b.Situacao! with { SituacaoVinculo = "TRC" } } },
            { "ano de ingresso", b with { AnoIngresso = 2021 } },
            { "período de ingresso", b with { PeriodoIngresso = 2 } },
        };
    }

    [Fact]
    public void Resumo_nao_cobre_o_cpf_para_nao_desfazer_a_cifra_em_repouso()
    {
        // O resumo fica guardado ao lado dos demais campos, legíveis na mesma linha. Se
        // cobrisse o CPF, quem tivesse a tabela conheceria todo o resto e poderia testar
        // os pouco mais de um bilhão de CPFs válidos até reproduzir o resumo — devolvendo
        // em claro o dado que a cifra protege.
        //
        // O preço está aqui declarado: correção isolada de CPF na origem não é percebida.
        string original = DecodificarUm(Completo()).ResumoDoConteudo;
        string comOutroCpf = DecodificarUm(Completo() with { Cpf = "11144477735" }).ResumoDoConteudo;

        comOutroCpf.Should().Be(
            original,
            "cobrir o CPF tornaria o resumo um caminho para recuperá-lo por tentativa e erro");
    }

    [Fact]
    public void Resumo_distingue_campo_ausente_de_campo_vazio()
    {
        VinculoDiscentePayload semCodigo = Completo() with
        {
            Curso = Completo().Curso! with { CodigoEmec = null },
        };
        VinculoDiscentePayload codigoVazio = Completo() with
        {
            Curso = Completo().Curso! with { CodigoEmec = "" },
        };

        string comAusente = DecodificarUm(semCodigo).ResumoDoConteudo;
        string comVazio = DecodificarUm(codigoVazio).ResumoDoConteudo;

        comVazio.Should().NotBe(comAusente, "ausência e vazio são estados diferentes na origem");
    }

    [Fact]
    public void Pagina_vazia_e_resposta_legitima()
    {
        ResultadoDaDecodificacao resultado = DecodificarPagina();

        resultado.Aceitos.Should().BeEmpty();
        resultado.Descartados.Should().BeEmpty();
    }

    private static VinculoDecodificado DecodificarUm(VinculoDiscentePayload unico) =>
        DecodificarPagina(unico).Aceitos[0];

    private static ResultadoDaDecodificacao DecodificarPagina(params VinculoDiscentePayload[] pagina) =>
        DecodificadorDeVinculos.Decodificar(pagina);

    /// <summary>
    /// Vínculo com todos os campos, nos moldes do exemplo que o contrato da origem publica.
    /// </summary>
    private static VinculoDiscentePayload Completo() => new()
    {
        IdDiscente = 24786,
        Matricula = "201446010001",
        Cpf = "52998224725",
        Nome = "FULANO DE TAL",
        Nivel = "G",
        Curso = new CursoPayload
        {
            Id = 42,
            Nome = "CIÊNCIA DA COMPUTAÇÃO",
            CodigoEmec = "1269997",
            Unidade = new UnidadePayload { Id = 12, Nome = "INSTITUTO DE CIENCIAS EXATAS" },
        },
        Situacao = new SituacaoPayload { Id = 1, Descricao = "ATIVO", SituacaoVinculo = "ATV" },
        AnoIngresso = 2020,
        PeriodoIngresso = 1,
        DateRequest = "2026-07-15 13:09:52",
    };
}
