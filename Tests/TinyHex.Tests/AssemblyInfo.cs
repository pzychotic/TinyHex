using System.Threading;

// The behavior tests instantiate WPF controls, which have thread affinity and need a
// single-threaded apartment, so run the whole test assembly on an STA thread.
[assembly: Apartment(ApartmentState.STA)]
