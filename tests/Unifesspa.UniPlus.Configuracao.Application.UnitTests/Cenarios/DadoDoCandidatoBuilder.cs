namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Cenarios;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

internal static class DadoDoCandidatoBuilder
{
    public static FatoCandidatoView Construir() => new(
        Guid.Empty, "COR", "Cor", "Cor informada", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao),
        CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.VariosValores), null, "INSCRICAO", "CAMPO:COR", null, null,
        true, CodigoComRotulo<ClassificacaoDeProtecao>.De(ClassificacaoDeProtecao.Pessoal), CodigoComRotulo<HipoteseLegalTratamento>.De(HipoteseLegalTratamento.Consentimento), "Inscrição", true);
}
