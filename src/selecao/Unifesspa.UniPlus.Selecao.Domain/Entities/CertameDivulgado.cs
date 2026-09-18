namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// O certame como o público o vê, materializado no instante em que ele passa a ser divulgável.
/// </summary>
/// <remarks>
/// <para>
/// <b>A existência desta linha É a publicidade do certame.</b> Ela nasce quando o ato normativo da
/// versão se confirma no registro central, e avança quando o ato de uma retificação se confirma.
/// Não há coluna de "visível": não existir e não ser público são a mesma coisa. É o que torna a
/// leitura pública uma consulta de tabela única.
/// </para>
/// <para>
/// <b>A retificação cujo ato não se confirma não avança a linha</b>, e o certame permanece no ar
/// com o conteúdo anterior — publicação é ato público, e para emendá-la existe retificação de ato,
/// não supressão.
/// </para>
/// <para>
/// Derivado e reconstruível: tudo sai da versão de configuração congelada, que permanece a fonte.
/// </para>
/// </remarks>
public sealed class CertameDivulgado : IIdentificavel
{
    private CertameDivulgado()
    {
    }

    /// <summary>O processo, que é também a identidade da linha: um certame, uma divulgação.</summary>
    public Guid Id { get; private init; }

    /// <summary>Versão de configuração que esta divulgação projeta.</summary>
    public int NumeroVersao { get; private set; }

    /// <summary>Ato normativo que criou a versão projetada — o que confirmou a publicidade.</summary>
    public Guid AtoCriadorId { get; private set; }

    /// <summary>
    /// Selo do conteúdo: resumo da configuração projetada, com a versão do formato desta projeção.
    /// </summary>
    public string HashConfiguracao { get; private set; } = null!;

    /// <summary>Versão do formato da projeção pública. Sobe quando a forma da resposta muda.</summary>
    public string VersaoProjecao { get; private set; } = null!;

    /// <summary>
    /// Título congelado do certame. Coluna, e não só campo do documento, porque a vitrine o busca e
    /// ordena por ele.
    /// </summary>
    public string Nome { get; private set; } = null!;

    /// <summary>Identificador legível do edital, quando a publicação o declara. A busca o alcança.</summary>
    public string? Numero { get; private set; }

    /// <summary>
    /// Códigos das modalidades com vaga no certame — o recorte por modalidade da vitrine corre
    /// sobre eles.
    /// </summary>
    public IReadOnlyList<string> ModalidadesOfertadas { get; private set; } = [];

    /// <summary>Abertura da janela de inscrição da versão projetada.</summary>
    public DateTimeOffset InscricoesDe { get; private set; }

    /// <summary>
    /// Encerramento da janela da versão projetada — a chave por que a vitrine ordena por padrão, e a
    /// grandeza de que as quatro situações derivam, junto com a abertura.
    /// </summary>
    public DateTimeOffset InscricoesAte { get; private set; }

    /// <summary>
    /// A resposta pública já pronta. Guardar o documento projetado, e não os campos em colunas,
    /// evita uma coluna nova a cada campo que o contrato ganhe — e a forma continua declarada, pelo
    /// tipo que a projetou.
    /// </summary>
    public string Certame { get; private set; } = null!;

    /// <summary>Instante em que esta divulgação foi materializada.</summary>
    public DateTimeOffset DivulgadoEm { get; private set; }

    public static CertameDivulgado Criar(
        Guid processoSeletivoId,
        int numeroVersao,
        Guid atoCriadorId,
        string hashConfiguracao,
        string versaoProjecao,
        FacetasDoCertameDivulgado facetas,
        string certame,
        DateTimeOffset divulgadoEm)
    {
        ArgumentNullException.ThrowIfNull(facetas);

        return new()
        {
            Id = processoSeletivoId,
            NumeroVersao = numeroVersao,
            AtoCriadorId = atoCriadorId,
            HashConfiguracao = hashConfiguracao,
            VersaoProjecao = versaoProjecao,
            Nome = facetas.Nome,
            Numero = facetas.Numero,
            // Cópia: o tipo do parâmetro é só leitura, mas a instância concreta é do chamador, e
            // uma mutação depois desta linha alteraria a coluna sem passar pela projeção — que é
            // a divergência entre faceta e documento que a materialização existe para impedir.
            ModalidadesOfertadas = [.. facetas.ModalidadesOfertadas],
            InscricoesDe = facetas.InscricoesDe,
            InscricoesAte = facetas.InscricoesAte,
            Certame = certame,
            DivulgadoEm = divulgadoEm,
        };
    }

    /// <summary>
    /// Avança a divulgação para uma versão mais nova. Recusa retroceder: a reentrega da mensagem de
    /// registro é esperada, e uma entrega atrasada do ato de uma versão ANTERIOR não pode desfazer
    /// uma retificação já divulgada.
    /// </summary>
    public bool TentarAvancar(
        int numeroVersao,
        Guid atoCriadorId,
        string hashConfiguracao,
        string versaoProjecao,
        FacetasDoCertameDivulgado facetas,
        string certame,
        DateTimeOffset divulgadoEm)
    {
        ArgumentNullException.ThrowIfNull(facetas);

        if (numeroVersao <= NumeroVersao)
        {
            return false;
        }

        NumeroVersao = numeroVersao;
        AtoCriadorId = atoCriadorId;
        HashConfiguracao = hashConfiguracao;
        VersaoProjecao = versaoProjecao;
        Nome = facetas.Nome;
        Numero = facetas.Numero;
        ModalidadesOfertadas = [.. facetas.ModalidadesOfertadas];
        InscricoesDe = facetas.InscricoesDe;
        InscricoesAte = facetas.InscricoesAte;
        Certame = certame;
        DivulgadoEm = divulgadoEm;
        return true;
    }
}
