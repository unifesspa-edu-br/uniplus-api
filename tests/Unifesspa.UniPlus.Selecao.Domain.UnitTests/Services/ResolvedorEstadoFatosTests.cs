namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Story #926 — o grafo de pré-condições resolvendo o estado de cada fato a partir das respostas
/// do candidato. A tabela de coleta usada aqui é a normativa da change (dez fatos, com o gate de
/// escola pública em todos os opt-ins de subcota exceto a dimensão PcD).
/// </summary>
public sealed class ResolvedorEstadoFatosTests
{
    private static JsonElement Sim => JsonSerializer.SerializeToElement(true);

    private static JsonElement Nao => JsonSerializer.SerializeToElement(false);

    private static JsonElement Cor(string valor) => JsonSerializer.SerializeToElement(valor);

    private static CondicaoPrecondicaoFato Cond(string fato, Operador operador, JsonElement valor) =>
        CondicaoPrecondicaoFato.Criar(0, fato, operador, valor).Value!;

    private static FatoColetado Fato(string codigo, int ordem, params CondicaoPrecondicaoFato[] precondicoes) =>
        FatoColetado.Criar(codigo, ordem, precondicoes).Value!;

    /// <summary>
    /// A tabela normativa de coleta. O gate de escola pública abre as subcotas; a dimensão PcD é a
    /// exceção — é coletada antes e não depende dele, porque a modalidade de pessoa com
    /// deficiência fora da reserva federal independe de o candidato ser egresso de escola pública.
    /// </summary>
    private static IReadOnlyList<FatoColetado> TabelaNormativa() =>
    [
        Fato("PCD", 0),
        Fato("EGRESSO_ESCOLA_PUBLICA", 1),
        Fato("CONCORRER_PCD", 2, Cond("PCD", Operador.Igual, Sim)),
        Fato("CONCORRER_EP", 3, Cond("EGRESSO_ESCOLA_PUBLICA", Operador.Igual, Sim)),
        Fato("COR_RACA", 4, Cond("EGRESSO_ESCOLA_PUBLICA", Operador.Igual, Sim)),
        Fato("CONCORRER_PPI", 5,
            Cond("EGRESSO_ESCOLA_PUBLICA", Operador.Igual, Sim),
            Cond("COR_RACA", Operador.Em, JsonSerializer.SerializeToElement(new[] { "INDIGENA", "PRETA", "PARDA" }))),
        Fato("QUILOMBOLA", 6,
            Cond("EGRESSO_ESCOLA_PUBLICA", Operador.Igual, Sim),
            Cond("COR_RACA", Operador.Diferente, Cor("INDIGENA"))),
        Fato("CONCORRER_Q", 7,
            Cond("EGRESSO_ESCOLA_PUBLICA", Operador.Igual, Sim),
            Cond("QUILOMBOLA", Operador.Igual, Sim)),
        Fato("BAIXA_RENDA", 8, Cond("EGRESSO_ESCOLA_PUBLICA", Operador.Igual, Sim)),
        Fato("CONCORRER_RENDA", 9,
            Cond("EGRESSO_ESCOLA_PUBLICA", Operador.Igual, Sim),
            Cond("BAIXA_RENDA", Operador.Igual, Sim)),
    ];

    private static IReadOnlyDictionary<string, FatoResolvido> Resolver(params (string Fato, JsonElement Valor)[] respostas) =>
        ResolvedorEstadoFatos.Resolver(
            TabelaNormativa(),
            respostas.ToDictionary(static r => r.Fato, static r => r.Valor, StringComparer.Ordinal));

    [Fact(DisplayName = "Opt-in condicionado à elegibilidade não é coletado quando a autodeclaração é negativa")]
    public void OptIn_SemElegibilidade_NaoAplicavel()
    {
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver(("PCD", Nao), ("EGRESSO_ESCOLA_PUBLICA", Nao));

        estados["CONCORRER_PCD"].Estado.Should().Be(
            EstadoFato.NaoAplicavel,
            "quem não se declara com deficiência não vê a pergunta sobre concorrer na cota PcD");
        estados["PCD"].Estado.Should().Be(EstadoFato.Resolvido);
    }

    [Fact(DisplayName = "Candidato não egresso de escola pública não vê nenhuma subcota, mas vê a dimensão PcD")]
    public void SemEscolaPublica_SubcotasNaoAplicaveis_MasPcdSegue()
    {
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver(("PCD", Sim), ("EGRESSO_ESCOLA_PUBLICA", Nao));

        foreach (string subcota in new[] { "CONCORRER_EP", "COR_RACA", "CONCORRER_PPI", "QUILOMBOLA", "CONCORRER_Q", "BAIXA_RENDA", "CONCORRER_RENDA" })
        {
            estados[subcota].Estado.Should().Be(EstadoFato.NaoAplicavel, $"'{subcota}' está atrás do gate de escola pública");
        }

        estados["CONCORRER_PCD"].Estado.Should().Be(
            EstadoFato.Indeterminado,
            "a dimensão PcD é a exceção ao gate — é perguntada, e aqui ainda não foi respondida");
    }

    [Fact(DisplayName = "Candidato indígena não vê o bloco quilombola, e isso não bloqueia o caminho PPI")]
    public void Indigena_BlocoQuilombolaSuprimido_CaminhoPpiSegue()
    {
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver(
            ("PCD", Nao), ("EGRESSO_ESCOLA_PUBLICA", Sim), ("COR_RACA", Cor("INDIGENA")));

        estados["QUILOMBOLA"].Estado.Should().Be(
            EstadoFato.NaoAplicavel,
            "a exclusão mútua entre indígena e quilombola é pré-condição do bloco, não código");
        estados["CONCORRER_Q"].Estado.Should().Be(
            EstadoFato.NaoAplicavel,
            "sem o bloco quilombola, o opt-in decorre não-aplicável — nunca fica pendente à espera de resposta");
        estados["CONCORRER_PPI"].Estado.Should().Be(
            EstadoFato.Indeterminado,
            "indígena está no domínio PPI: a pergunta é feita e segue aberta, sem ser bloqueada pela supressão do bloco quilombola");
    }

    [Fact(DisplayName = "Candidato com cor/raça não informada VÊ o bloco quilombola — não informar não é declarar-se indígena")]
    public void CorRacaNaoInformada_VeBlocoQuilombola()
    {
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver(
            ("PCD", Nao), ("EGRESSO_ESCOLA_PUBLICA", Sim), ("COR_RACA", Cor("NAO_INFORMADO")));

        estados["QUILOMBOLA"].Estado.Should().Be(
            EstadoFato.Indeterminado,
            "NAO_INFORMADO é diferente de INDIGENA, então a pré-condição é verdadeira e o campo é apresentado");
        estados["CONCORRER_PPI"].Estado.Should().Be(
            EstadoFato.NaoAplicavel,
            "NAO_INFORMADO não pertence ao domínio PPI — aí sim o opt-in não se aplica");
    }

    [Fact(DisplayName = "Pré-condição satisfeita e campo não respondido fica pendente, nunca dispensado")]
    public void PrecondicaoSatisfeita_SemResposta_Indeterminado()
    {
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver(("PCD", Sim), ("EGRESSO_ESCOLA_PUBLICA", Sim));

        estados["CONCORRER_PCD"].Estado.Should().Be(
            EstadoFato.Indeterminado,
            "o candidato é PcD e a pergunta lhe foi feita — enquanto não responder, a exigência que depende disso segue em aberto");
    }

    [Fact(DisplayName = "Pré-condição indeterminada propaga indeterminação, não inaplicabilidade")]
    public void PrecondicaoIndeterminada_Propaga()
    {
        // EGRESSO_ESCOLA_PUBLICA não respondido: tudo que está atrás do gate fica em suspenso.
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver(("PCD", Sim));

        estados["COR_RACA"].Estado.Should().Be(
            EstadoFato.Indeterminado,
            "decidir por não-aplicável aqui dispensaria em definitivo um campo que ainda pode vir a ser exigido");
        estados["CONCORRER_Q"].Estado.Should().Be(
            EstadoFato.Indeterminado,
            "a indeterminação atravessa a cadeia inteira de dependências");
    }

    [Fact(DisplayName = "Resposta de campo que deixou de ser aplicável não é aproveitada")]
    public void RespostaObsoleta_NaoEhAproveitada()
    {
        // O candidato havia se declarado egresso e respondido o bloco quilombola; depois mudou a
        // autodeclaração de escola pública para negativa. A resposta antiga continua no insumo.
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver(
            ("PCD", Nao),
            ("EGRESSO_ESCOLA_PUBLICA", Nao),
            ("QUILOMBOLA", Sim),
            ("CONCORRER_Q", Sim));

        estados["QUILOMBOLA"].Estado.Should().Be(
            EstadoFato.NaoAplicavel,
            "a pré-condição passou a ser falsa — a resposta anterior não sobrevive à mudança a montante");
        estados["QUILOMBOLA"].Valor.Should().BeNull("um fato não-aplicável nunca carrega valor");
        estados["CONCORRER_Q"].Estado.Should().Be(EstadoFato.NaoAplicavel);
    }

    [Fact(DisplayName = "Resposta de fato que o processo não coleta é ignorada — o grafo é a autoridade")]
    public void RespostaOrfa_Ignorada()
    {
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver(
            ("PCD", Nao), ("EGRESSO_ESCOLA_PUBLICA", Nao), ("FATO_QUE_NAO_EXISTE", Sim));

        estados.Should().NotContainKey(
            "FATO_QUE_NAO_EXISTE",
            "uma resposta órfã não pode criar um fato que a configuração do processo não prevê");
        estados.Should().HaveCount(10, "o resultado tem exatamente os fatos coletados pelo processo");
    }

    [Fact(DisplayName = "O resolvedor avalia na ordem de coleta, não na ordem em que os fatos chegam na coleção")]
    public void Resolver_RespeitaOrdemDeColeta_NaoOrdemDaColecao()
    {
        // A coleção chega embaralhada: CONCORRER_PCD (ordem 2, depende de PCD) vem ANTES de PCD
        // (ordem 0) na lista. O candidato respondeu PCD = NÃO.
        //
        // Ordem CERTA (por coleta): PCD resolve primeiro, com valor NÃO; a pré-condição
        // PCD IGUAL SIM avalia falso; CONCORRER_PCD é NÃO_APLICÁVEL.
        // Ordem ERRADA (por coleção): CONCORRER_PCD é avaliado com PCD ainda ausente; a
        // pré-condição fica indeterminada e CONCORRER_PCD resolve INDETERMINADO.
        // Os dois estados são distintos — é isso que dá poder de detecção ao teste.
        IReadOnlyList<FatoColetado> embaralhado =
        [
            Fato("CONCORRER_PCD", 2, Cond("PCD", Operador.Igual, Sim)),
            Fato("EGRESSO_ESCOLA_PUBLICA", 1),
            Fato("PCD", 0),
        ];

        IReadOnlyDictionary<string, FatoResolvido> estados = ResolvedorEstadoFatos.Resolver(
            embaralhado,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["PCD"] = Nao });

        estados["CONCORRER_PCD"].Estado.Should().Be(
            EstadoFato.NaoAplicavel,
            "PCD já está resolvido (=NÃO) quando o opt-in é avaliado; avaliar na ordem da coleção o deixaria indeterminado por dependência ainda não vista");
    }

    [Fact(DisplayName = "Fato sem pré-condição é sempre coletado")]
    public void SemPrecondicao_SempreColetado()
    {
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver();

        estados["PCD"].Estado.Should().Be(EstadoFato.Indeterminado, "é perguntado a todos — aqui apenas ainda não respondido");
        estados["EGRESSO_ESCOLA_PUBLICA"].Estado.Should().Be(EstadoFato.Indeterminado);
    }

    [Fact(DisplayName = "Perfil completo de cotista: todos os blocos abertos resolvem com valor")]
    public void PerfilCompleto_TudoResolvido()
    {
        IReadOnlyDictionary<string, FatoResolvido> estados = Resolver(
            ("PCD", Nao),
            ("EGRESSO_ESCOLA_PUBLICA", Sim),
            ("COR_RACA", Cor("PARDA")),
            ("CONCORRER_EP", Nao),
            ("CONCORRER_PPI", Sim),
            ("QUILOMBOLA", Nao),
            ("BAIXA_RENDA", Sim),
            ("CONCORRER_RENDA", Sim));

        estados["CONCORRER_PPI"].Estado.Should().Be(EstadoFato.Resolvido);
        estados["CONCORRER_RENDA"].Estado.Should().Be(EstadoFato.Resolvido);
        estados["CONCORRER_Q"].Estado.Should().Be(
            EstadoFato.NaoAplicavel,
            "não se declarou quilombola, então o opt-in correspondente não é apresentado");
        estados["CONCORRER_PCD"].Estado.Should().Be(EstadoFato.NaoAplicavel);
    }
}
