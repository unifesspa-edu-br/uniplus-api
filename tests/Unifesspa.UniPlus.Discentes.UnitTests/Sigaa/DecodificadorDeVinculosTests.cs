namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

using static Unifesspa.UniPlus.Discentes.UnitTests.Sigaa.VinculosDaOrigem;

public sealed class DecodificadorDeVinculosTests
{
    [Fact]
    public void Traduz_vinculo_completo_com_todos_os_campos()
    {
        ResultadoDaDecodificacao resultado = Decodificar(Completo());

        resultado.Aceitos.Should().HaveCount(1);
        resultado.Descartados.Should().BeEmpty();

        VinculoDiscenteCopiado dadosCopiados = resultado.Aceitos[0].Vinculo.DadosCopiados;
        dadosCopiados.Identificacao.IdDiscenteSigaa.Valor.Should().Be(24786);
        dadosCopiados.Identificacao.Matricula.Valor.Should().Be("201446010001");
        dadosCopiados.Identificacao.Cpf.Valor.Should().Be("52998224725");
        dadosCopiados.Nivel.Valor.Should().Be("G");
        dadosCopiados.Curso.Nome.Valor.Should().Be("CIÊNCIA DA COMPUTAÇÃO");
        dadosCopiados.Curso.CodigoEmec!.Valor.Should().Be("1269997");
        dadosCopiados.Curso.Unidade.Nome.Valor.Should().Be("INSTITUTO DE CIENCIAS EXATAS");
        dadosCopiados.Ingresso.Ano.Should().Be(2020);
    }

    [Fact]
    public void Reflete_a_situacao_como_a_origem_a_entrega()
    {
        ResultadoDaDecodificacao resultado = Decodificar(Completo());

        SituacaoAcademicaCopiada situacao = resultado.Aceitos[0].Vinculo.DadosCopiados.Situacao;
        situacao.Id.Valor.Should().Be(1);
        situacao.Descricao.Valor.Should().Be("ATIVO", "a situação é espelhada, não traduzida");
        situacao.Qualificador!.Valor.Should().Be("ATV");
    }

    /// <summary>
    /// É o caso de mais de mil vínculos da origem. Descartar é decisão registrada;
    /// interromper a leitura por causa deles pararia a sincronização todo dia.
    /// </summary>
    [Fact]
    public void Descarta_vinculo_de_curso_sem_unidade_academica_e_segue()
    {
        ResultadoDaDecodificacao resultado = Decodificar(SemUnidadeAcademica());

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
        VinculoDiscentePayload semIngresso = Completo() with { AnoIngresso = ano, PeriodoIngresso = periodo };

        ResultadoDaDecodificacao resultado = Decodificar(semIngresso);

        resultado.Aceitos.Should().BeEmpty();
        resultado.Descartados.Should().ContainSingle()
            .Which.Motivo.Should().Be(MotivoDeDescarte.SemPeriodoDeIngresso);
    }

    [Fact]
    public void Descarte_nao_contamina_os_vizinhos_da_mesma_pagina()
    {
        VinculoDiscentePayload descartavel = SemUnidadeAcademica() with { IdDiscente = 99 };

        ResultadoDaDecodificacao resultado = Decodificar(
            Completo(), descartavel, Completo() with { IdDiscente = 77 });

        resultado.Aceitos.Should().HaveCount(2, "os vínculos completos continuam entrando");
        resultado.Descartados.Should().ContainSingle()
            .Which.IdDiscenteSigaa.Should().Be(99);
    }

    /// <summary>
    /// O contraste do teste da página inteira sem CPF: descarte por registro incompleto é
    /// rotina, tem volume conhecido, e não deve acionar suspeita de contrato rompido.
    /// </summary>
    [Fact]
    public void Registro_incompleto_nao_conta_como_entrega_fora_do_combinado()
    {
        ResultadoDaDecodificacao resultado = Decodificar(
            SemUnidadeAcademica(),
            Completo() with { AnoIngresso = null });

        resultado.Descartados.Should().HaveCount(2);
        resultado.QuantidadeForaDoContrato.Should().Be(0);
    }

    [Fact]
    public void Pagina_vazia_e_resposta_legitima()
    {
        ResultadoDaDecodificacao resultado = Decodificar();

        resultado.Aceitos.Should().BeEmpty();
        resultado.Descartados.Should().BeEmpty();
    }
}
