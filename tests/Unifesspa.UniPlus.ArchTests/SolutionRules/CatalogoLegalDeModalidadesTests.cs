namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Amarra as duas listas de códigos de modalidade legal que a solução mantém: o catálogo
/// protegido em Configuração (<see cref="CodigoModalidade.CodigosLegaisFixos"/>), que
/// recusa edição e remoção por cadastro, e o conjunto exigido em Seleção
/// (<see cref="ModalidadesFederaisLei12711"/>), que a distribuição de vagas obriga quando
/// o edital aplica a Lei 12.711/2012.
/// </summary>
/// <remarks>
/// As duas listas existem separadas de propósito: Configuração é o catálogo e Seleção é a
/// consumidora, e um módulo não referencia o domínio do outro. A cópia é aceitável porque
/// a lei é o contrato comum — mas só enquanto não divergirem. Um código renomeado de um
/// lado e não do outro produziria um edital que exige uma modalidade que o catálogo não
/// tem, e o erro só apareceria na hora de publicar.
/// </remarks>
public sealed class CatalogoLegalDeModalidadesTests
{
    [Fact(DisplayName = "Todo código exigido pela distribuição da Lei 12.711 está protegido no catálogo")]
    public void CodigosDeSelecao_SaoSubconjuntoDoCatalogoProtegido() =>
        ModalidadesFederaisLei12711.CodigosComAc.Should().BeSubsetOf(
            CodigoModalidade.CodigosLegaisFixos,
            "um código que a distribuição de vagas exige mas o catálogo não protege pode "
            + "ser apagado por cadastro, inviabilizando configurar a lei depois");

    [Fact(DisplayName = "O catálogo protegido acrescenta as quatro modalidades institucionais fora da reserva federal")]
    public void CatalogoProtegido_ExcedeSelecaoNasModalidadesInstitucionais()
    {
        string[] exclusivosDoCatalogo = [.. CodigoModalidade.CodigosLegaisFixos
            .Except(ModalidadesFederaisLei12711.CodigosComAc, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        exclusivosDoCatalogo.Should().Equal(
            [CodigoModalidade.AcI, CodigoModalidade.AcPcd, CodigoModalidade.AcQ, CodigoModalidade.PcdPuro],
            "nenhuma das quatro entra na cascata das oito federais — AC_PCD e PCD_PURO retiram "
            + "vaga da ampla concorrência, AC_I e AC_Q somam ao total do curso —, mas todas são "
            + "reserva fixada por norma institucional, e a proteção do catálogo vale para as quatro");
    }
}
