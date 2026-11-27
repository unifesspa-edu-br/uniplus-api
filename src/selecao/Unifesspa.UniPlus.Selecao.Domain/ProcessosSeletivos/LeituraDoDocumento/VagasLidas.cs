namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As vagas que a leitura do documento entregou como declaração (as opções de curso, a distribuição de cada oferta, a cascata, o
/// Fator de Atenuação das Desigualdades de Acesso e o atendimento especializado) e a origem que guarda a posição enviada de cada
/// parte e as partes que a leitura recusou. Cada parte é lida pelas regras da própria entidade, e a que a leitura recusa fica fora
/// da declaração sem impedir a leitura das outras. O bloco ausente é a declaração vazia, que a conformidade da configuração confere.
/// </summary>
public sealed record VagasLidas(VagasDeclaradas Vagas, OrigemNoDocumento Origem)
{
    /// <summary>Lê as vagas do documento nos cadastros citados e acumula as recusas, por bloco, na ordem do documento.</summary>
    public static VagasLidas Ler(DeclaracoesDoDocumento declaracoes, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(declaracoes);
        ArgumentNullException.ThrowIfNull(cadastros);

        return declaracoes.Vagas is { } vagas
            ? LerBloco(vagas, cadastros)
            : new(new([], null, null, null, null, null), OrigemNoDocumento.Identidade);
    }

    /// <summary>
    /// As distribuições que a leitura adotou, cada uma com a posição que tinha na lista enviada; a que a leitura recusou não aparece.
    /// </summary>
    public IReadOnlyList<(int PosicaoNoDocumento, ConfiguracaoDistribuicaoVagas Distribuicao)> DistribuicoesNaPosicaoDoDocumento() =>
        [.. Vagas.DistribuicaoVagas.Select((distribuicao, canonica) =>
            (Origem.PosicaoDeOrigem(LeituraDasDistribuicoesDeVagas.Lista, canonica), distribuicao))];

    private static VagasLidas LerBloco(VagasNoDocumento vagas, CadastrosCitadosNoDocumento cadastros)
    {
        OpcoesDeCursoLidas opcoes = LeituraDasOpcoesDeCurso.Ler(vagas.OpcoesDeCurso);
        DistribuicoesLidas distribuicoes = LeituraDasDistribuicoesDeVagas.Ler(vagas.Distribuicoes, cadastros);
        CascataLida cascata = LeituraDaCascataDeRemanejamento.Ler(vagas.Cascata, cadastros);
        AtendimentoLido atendimento = LeituraDoAtendimento.Ler(vagas.Atendimento, cadastros);
        FadaLido fada = LeituraDoFada.Ler(vagas.Fada, cadastros);

        return new(
            Declaradas(opcoes, distribuicoes, cascata, atendimento, fada),
            OrigemNoDocumento.Reunir([opcoes.Origem(), distribuicoes.Origem(), cascata.Origem(), atendimento.Origem(), fada.Origem()]));
    }

    private static VagasDeclaradas Declaradas(
        OpcoesDeCursoLidas opcoes, DistribuicoesLidas distribuicoes, CascataLida cascata, AtendimentoLido atendimento, FadaLido fada) => new(
        [.. distribuicoes.Adotadas.Select(static adotada => adotada.Distribuicao!)],
        opcoes.Numero,
        fada.Aplica,
        fada.Fada,
        cascata.Cascata,
        atendimento.Atendimento);
}
