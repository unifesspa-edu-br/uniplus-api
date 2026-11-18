namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>A quantidade de vagas que o quadro de uma oferta reserva a uma modalidade.</summary>
public sealed record VagasDaModalidade(string ModalidadeCodigo, int Quantidade);
